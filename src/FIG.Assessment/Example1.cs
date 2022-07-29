namespace FIG.Assessment;

/// <summary>
/// In this example, the goal of this GetPeopleInfo method is to fetch a list of Person IDs from our database (not implemented / not part of this example),
/// and for each Person ID we need to hit some external API for information about the person. Then we would like to return a dictionary of the results to the caller.
/// We want to perform this work in parallel to speed it up, but we don't want to hit the API too frequently, so we are trying to limit to 5 requests at a time at most
/// by using 5 background worker threads.
/// Feel free to suggest changes to any part of this example.
/// In addition to finding issues and/or ways of improving this method, what is a name for this sort of queueing pattern?
/// </summary>
public class Example1
{
    public Dictionary<int, int> GetPeopleInfo()
    {
        // initialize empty queue, and empty result set
        var personIdQueue = new Queue<int>();
        var results = new Dictionary<int, int>();

        // start thread which will populate the queue
        var collectThread = new Thread(() => CollectPersonIds(personIdQueue));
        collectThread.Start();

        // start 5 worker threads to read through the queue and fetch info on each item, adding to the result set
        var gatherThreads = new List<Thread>();
        for (var i = 0; i < 5; i++)
        {
            var gatherThread = new Thread(() => GatherInfo(personIdQueue, results));
            gatherThread.Start();
            gatherThreads.Add(gatherThread);
        }

        // wait for all threads to finish
        collectThread.Join();
        foreach (var gatherThread in gatherThreads)
            gatherThread.Join();

        return results;
    }

    private void CollectPersonIds(Queue<int> personIdQueue)
    {
        // dummy implementation, would be pulling from a database
        for (var i = 1; i < 100; i++)
        {
            if (i % 10 == 0) Thread.Sleep(TimeSpan.FromMilliseconds(50)); // artificial delay every now and then
            personIdQueue.Enqueue(i);
        }
    }

    private void GatherInfo(Queue<int> personIdQueue, Dictionary<int, int> results)
    {
        // pull IDs off the queue until it is empty
        while (personIdQueue.TryDequeue(out var id))
        {
            var client = new HttpClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://some.example.api/people/{id}/age");
            var response = client.SendAsync(request).Result;
            var age = int.Parse(response.Content.ReadAsStringAsync().Result);
            results[id] = age;
        }
    }
}
