using BetterHaveIt.DataStructures;
using Microsoft.Extensions.Configuration;
using SolmangoCLI.DecentralizedActivities;
using SolmangoNET.Rpc;

namespace SolmangoCLI.Services;

internal class ActivityProvider : IActivityProvider
{
    private readonly DistinctList<DecentralizedActivity> activities;

    public ActivityProvider(IConfiguration configuration)
    {
        activities = new DistinctList<DecentralizedActivity>();
        RegisterActivity(new DummyActivity(configuration));
    }

    public DecentralizedActivity GetActivity(string activityId) => activities.Find(a => a.Id.Equals(activityId));

    public void RegisterActivity(DecentralizedActivity activity) => activities.Add(activity);
}