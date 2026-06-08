using SQLite;
using FitnessTracker.Models;
using System.Text.Json;
using System.Linq;

namespace FitnessTracker.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection _connection;

    private async Task Init()
    {
        if (_connection != null)
            return;

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "fitness.db3");
        _connection = new SQLiteAsyncConnection(dbPath);

        await _connection.CreateTableAsync<Workout>();

        try
        {
            await _connection.ExecuteAsync(
                "ALTER TABLE Workout ADD COLUMN StravaActivityId INTEGER NULL");
        }
        catch
        {
            // Kolumna już istnieje - ignorujemy
        }
    }

    public async Task<(double? MaxWeight, DateTime? Date, long? WorkoutId)> GetExerciseRecordAsync(string exercise)
    {
        var workouts = await GetWorkoutsAsync();

        double? maxWeight = null;
        DateTime? recordDate = null;
        long? workoutId = null;

        foreach (var workout in workouts.Where(w => w.Type == "Siłownia" && w.Exercise == exercise))
        {
            var sets = DeserializeGymSets(workout.GymSetsJson);
            if (sets.Count == 0) continue;

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

    public async Task<List<(string Exercise, double MaxWeight, DateTime Date)>> GetAllExerciseRecordsAsync()
    {
        var workouts = await GetWorkoutsAsync();

        var result = workouts
            .Where(w => w.Type == "Siłownia" && !string.IsNullOrWhiteSpace(w.Exercise))
            .Select(w => new
            {
                Workout = w,
                Sets = DeserializeGymSets(w.GymSetsJson)
            })
            .Where(x => x.Sets.Count > 0)
            .GroupBy(x => x.Workout.Exercise!)
            .Select(g =>
            {
                var best = g.OrderByDescending(x => x.Sets.Max(s => s.Weight)).First();
                return (
                    Exercise: g.Key,
                    MaxWeight: g.Max(x => x.Sets.Max(s => s.Weight)),
                    Date: best.Workout.Date
                );
            })
            .OrderByDescending(x => x.MaxWeight)
            .ThenBy(x => x.Exercise)
            .ToList();

        return result;
    }

    private static List<WorkoutSet> DeserializeGymSets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<List<WorkoutSet>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
    


    public async Task DeleteWorkoutAsync(Workout workout)
    {
        await Init();

        if (workout == null)
            return;

        await _connection.DeleteAsync(workout);
    }

    public async Task UpdateWorkoutAsync(Workout workout)
    {
        await Init();
        await _connection.UpdateAsync(workout);
    }

    public async Task SaveWorkoutAsync(Workout workout)
    {
        await Init();
        await _connection.InsertAsync(workout);
    }

    public async Task<bool> ExistsByStravaActivityIdAsync(long stravaActivityId)
    {
        await Init();

        var existing = await _connection.Table<Workout>()
            .Where(w => w.StravaActivityId == stravaActivityId)
            .FirstOrDefaultAsync();

        return existing != null;
    }

    public async Task<List<Workout>> GetWorkoutsAsync()
    {
        await Init();
        return await _connection.Table<Workout>()
            .OrderByDescending(x => x.Date)
            .ToListAsync();
    }

    public async Task<List<Workout>> GetPersonalRecordsAsync()
    {
        await Init();

        var allGymWorkouts = await _connection.Table<Workout>()
            .Where(w => w.Type == "Siłownia")
            .ToListAsync();

        var records = new List<Workout>();

        var groupedByExercise = allGymWorkouts
            .Where(w => !string.IsNullOrEmpty(w.Exercise))
            .GroupBy(w => w.Exercise);

        foreach (var group in groupedByExercise)
        {
            double maxWeight = 0;
            Workout bestWorkout = null;

            foreach (var workout in group)
            {
                if (!string.IsNullOrEmpty(workout.GymSetsJson))
                {
                    try
                    {
                        var sets = JsonSerializer.Deserialize<List<WorkoutSet>>(workout.GymSetsJson);

                        if (sets != null && sets.Any())
                        {
                            var workoutMax = sets.Max(s => s.Weight);

                            if (workoutMax >= maxWeight)
                            {
                                maxWeight = workoutMax;
                                bestWorkout = workout;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (bestWorkout != null)
            {
                var recordWorkout = new Workout
                {
                    Category = bestWorkout.Category,
                    Exercise = bestWorkout.Exercise,
                    GymSetsJson = bestWorkout.GymSetsJson
                };

                records.Add(recordWorkout);
            }
        }

        return records.OrderBy(w => w.Category).ToList();
    }
}