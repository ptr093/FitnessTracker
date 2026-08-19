using SQLite;
using FitnessTracker.Models;
using System.Text.Json;
using System.Linq;

namespace FitnessTracker.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _connection;

    private async Task Init()
    {
        if (_connection != null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "fitness2.db3");

        _connection = new SQLiteAsyncConnection(dbPath);

        // Istniejąca tabela treningów.
        await _connection.CreateTableAsync<Workout>();

        // Nowe tabele szablonów.
        await _connection.CreateTableAsync<WorkoutTemplate>();
        await _connection.CreateTableAsync<WorkoutTemplateExercise>();

        // Migracje istniejącej tabeli Workout.
        await TryAddColumnAsync(
            "ALTER TABLE Workout ADD COLUMN StravaActivityId INTEGER NULL");

        await TryAddColumnAsync(
            "ALTER TABLE Workout ADD COLUMN Location TEXT NULL");
    }

    private async Task TryAddColumnAsync(string sql)
    {
        try
        {
            await _connection!.ExecuteAsync(sql);
        }
        catch
        {
            // Przy kolejnych uruchomieniach kolumna istnieje już w tabeli.
        }
    }

    // =========================================================
    // TRENINGI
    // =========================================================

    public async Task SaveWorkoutAsync(Workout workout)
    {
        await Init();

        if (workout == null)
            throw new ArgumentNullException(nameof(workout));

        await _connection!.InsertAsync(workout);
    }

    public async Task UpdateWorkoutAsync(Workout workout)
    {
        await Init();

        if (workout == null)
            throw new ArgumentNullException(nameof(workout));

        await _connection!.UpdateAsync(workout);
    }

    public async Task DeleteWorkoutAsync(Workout workout)
    {
        await Init();

        if (workout == null)
            return;

        await _connection!.DeleteAsync(workout);
    }

    public async Task<List<Workout>> GetWorkoutsAsync()
    {
        await Init();

        return await _connection!
            .Table<Workout>()
            .OrderByDescending(x => x.Date)
            .ToListAsync();
    }

    public async Task<bool> ExistsByStravaActivityIdAsync(long stravaActivityId)
    {
        await Init();

        var existing = await _connection!
            .Table<Workout>()
            .Where(w => w.StravaActivityId == stravaActivityId)
            .FirstOrDefaultAsync();

        return existing != null;
    }

    // =========================================================
    // SZABLONY TRENINGÓW
    // =========================================================

    public async Task<List<WorkoutTemplate>> GetWorkoutTemplatesAsync()
    {
        await Init();

        return await _connection!
            .Table<WorkoutTemplate>()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    public async Task<WorkoutTemplate?> GetWorkoutTemplateAsync(int templateId)
    {
        await Init();

        return await _connection!
            .Table<WorkoutTemplate>()
            .Where(x => x.Id == templateId)
            .FirstOrDefaultAsync();
    }

    public async Task<List<WorkoutTemplateExercise>> GetTemplateExercisesAsync(
        int templateId)
    {
        await Init();

        return await _connection!
            .Table<WorkoutTemplateExercise>()
            .Where(x => x.TemplateId == templateId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();
    }

    public async Task<int> SaveWorkoutTemplateAsync(
        WorkoutTemplate template,
        IEnumerable<WorkoutTemplateExercise> exercises)
    {
        await Init();

        if (template == null)
            throw new ArgumentNullException(nameof(template));

        var exerciseList = exercises?.ToList()
                           ?? new List<WorkoutTemplateExercise>();

        template.Name = template.Name.Trim();
        template.Description = string.IsNullOrWhiteSpace(template.Description)
            ? null
            : template.Description.Trim();

        template.UpdatedAt = DateTime.Now;

        if (template.Id == 0)
        {
            template.CreatedAt = DateTime.Now;
            await _connection!.InsertAsync(template);
        }
        else
        {
            await _connection!.UpdateAsync(template);

            await _connection.ExecuteAsync(
                "DELETE FROM WorkoutTemplateExercises WHERE TemplateId = ?",
                template.Id);
        }

        for (var index = 0; index < exerciseList.Count; index++)
        {
            var item = exerciseList[index];

            item.Id = 0;
            item.TemplateId = template.Id;
            item.SortOrder = index;
            item.Category = item.Category.Trim();
            item.Exercise = item.Exercise.Trim();
            item.DefaultSetCount = Math.Max(1, item.DefaultSetCount);
        }

        if (exerciseList.Count > 0)
        {
            await _connection!.InsertAllAsync(exerciseList);
        }

        return template.Id;
    }

    public async Task DeleteWorkoutTemplateAsync(int templateId)
    {
        await Init();

        await _connection!.ExecuteAsync(
            "DELETE FROM WorkoutTemplateExercises WHERE TemplateId = ?",
            templateId);

        await _connection.ExecuteAsync(
            "DELETE FROM WorkoutTemplates WHERE Id = ?",
            templateId);
    }

    public async Task<List<WorkoutSet>> GetLastGymSetsForExerciseAsync(
        string exercise)
    {
        await Init();

        if (string.IsNullOrWhiteSpace(exercise))
            return new List<WorkoutSet>();

        var lastWorkout = await _connection!
            .Table<Workout>()
            .Where(x => x.Type == "Siłownia" && x.Exercise == exercise)
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync();

        if (lastWorkout == null)
            return new List<WorkoutSet>();

        return DeserializeGymSets(lastWorkout.GymSetsJson)
            .OrderBy(x => x.SetNumber)
            .Select((set, index) => new WorkoutSet
            {
                SetNumber = index + 1,
                Weight = set.Weight,
                Reps = set.Reps
            })
            .ToList();
    }
    // =========================================================
    // REKORDY
    // =========================================================

    public async Task<(double? MaxWeight, DateTime? Date, long? WorkoutId)>
        GetExerciseRecordAsync(string exercise)
    {
        var workouts = await GetWorkoutsAsync();

        double? maxWeight = null;
        DateTime? recordDate = null;
        long? workoutId = null;

        foreach (var workout in workouts.Where(w =>
                     w.Type == "Siłownia" &&
                     w.Exercise == exercise))
        {
            var sets = DeserializeGymSets(workout.GymSetsJson);

            if (sets.Count == 0)
                continue;

            var localMax = sets.Max(s => s.Weight);

            if (maxWeight == null || localMax > maxWeight.Value)
            {
                maxWeight = localMax;
                recordDate = workout.Date;
                workoutId = workout.Id;
            }
        }

        return (maxWeight, recordDate, workoutId);
    }

    public async Task<List<(string Exercise, double MaxWeight, DateTime Date)>>
        GetAllExerciseRecordsAsync()
    {
        var workouts = await GetWorkoutsAsync();

        var result = workouts
            .Where(w => w.Type == "Siłownia" &&
                        !string.IsNullOrWhiteSpace(w.Exercise))
            .Select(w => new
            {
                Workout = w,
                Sets = DeserializeGymSets(w.GymSetsJson)
            })
            .Where(x => x.Sets.Count > 0)
            .GroupBy(x => x.Workout.Exercise!)
            .Select(group =>
            {
                var maxWeight = group.Max(x => x.Sets.Max(s => s.Weight));

                var workoutWithRecord = group
                    .Where(x => x.Sets.Max(s => s.Weight) == maxWeight)
                    .OrderByDescending(x => x.Workout.Date)
                    .First();

                return (
                    Exercise: group.Key,
                    MaxWeight: maxWeight,
                    Date: workoutWithRecord.Workout.Date
                );
            })
            .OrderByDescending(x => x.MaxWeight)
            .ThenBy(x => x.Exercise)
            .ToList();

        return result;
    }

    public async Task<List<Workout>> GetPersonalRecordsAsync()
    {
        await Init();

        var allGymWorkouts = await _connection!
            .Table<Workout>()
            .Where(w => w.Type == "Siłownia")
            .ToListAsync();

        var records = new List<Workout>();

        var groupedByExercise = allGymWorkouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Exercise))
            .GroupBy(w => w.Exercise);

        foreach (var group in groupedByExercise)
        {
            var bestWorkout = group
                .Select(workout => new
                {
                    Workout = workout,
                    Sets = DeserializeGymSets(workout.GymSetsJson)
                })
                .Where(x => x.Sets.Count > 0)
                .OrderByDescending(x => x.Sets.Max(set => set.Weight))
                .ThenByDescending(x => x.Workout.Date)
                .FirstOrDefault();

            if (bestWorkout == null)
                continue;

            records.Add(new Workout
            {
                Category = bestWorkout.Workout.Category,
                Exercise = bestWorkout.Workout.Exercise,
                GymSetsJson = bestWorkout.Workout.GymSetsJson,
                Date = bestWorkout.Workout.Date,
                Location = bestWorkout.Workout.Location
            });
        }

        return records
            .OrderBy(w => w.Category)
            .ThenBy(w => w.Exercise)
            .ToList();
    }

    // =========================================================
    // JSON SERII
    // =========================================================

    private static List<WorkoutSet> DeserializeGymSets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<WorkoutSet>();

        try
        {
            return JsonSerializer.Deserialize<List<WorkoutSet>>(json)
                   ?? new List<WorkoutSet>();
        }
        catch
        {
            return new List<WorkoutSet>();
        }
    }
}