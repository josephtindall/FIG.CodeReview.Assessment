using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace FIG.Assessment;

public class Example1(ILogger logger)
{
    private static readonly HttpClient HttpClient = new();

    /// <summary>
    ///     Fetches a list of Person IDs and retrieves their age information via an external API.
    /// </summary>
    /// <param name="cancellationToken">Token for canceling the operation.</param>
    /// <returns>A dictionary mapping Person IDs to their ages.</returns>
    public async Task<Dictionary<int, int>> GetPeopleInfoAsync(CancellationToken cancellationToken = default)
    {
        var personIdQueue = new ConcurrentQueue<int>();
        var results = new ConcurrentDictionary<int, int>();

        await CollectPersonIdsAsync(personIdQueue);

        await Parallel.ForEachAsync(
            personIdQueue,
            new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = cancellationToken },
            async (id, _) =>
            {
                try
                {
                    var age = await FetchPersonAgeAsync(id, cancellationToken);
                    if (age != -1) results[id] = age;
                }
                catch (Exception ex)
                {
                    logger.LogError($"Error processing ID {id}: {ex.Message}");
                }
            });

        return new Dictionary<int, int>(results);
    }


    /// <summary>
    ///     Collects Person IDs from a database and enqueues them for processing.
    /// </summary>
    /// <param name="personIdQueue">Queue to store Person IDs.</param>
    private async Task CollectPersonIdsAsync(ConcurrentQueue<int> personIdQueue)
    {
        for (var i = 1; i < 100; i++)
        {
#if DEBUG
            if (i % 10 == 0)
            {
                logger.LogDebug("Simulating database delay for ID {Id}", i);
                await Task.Delay(50);
            }
#endif
            personIdQueue.Enqueue(i);
        }
    }

    /// <summary>
    ///     Calls the external API to fetch a person's age.
    /// </summary>
    /// <param name="id">Person ID.</param>
    /// <param name="cancellationToken">Token for canceling the operation.</param>
    /// <returns>Age of the person.</returns>
    private async Task<int> FetchPersonAgeAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await HttpClient.GetStreamAsync(
                $"https://some.example.api/people/{id}/age",
                cancellationToken);

            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(cancellationToken);

            if (int.TryParse(content, out var age))
                return age;

            logger.LogWarning("Invalid age format received for ID {Id}: {Content}", id, content);
            return -1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching age for ID {Id}", id);
            return -1;
        }
    }
}