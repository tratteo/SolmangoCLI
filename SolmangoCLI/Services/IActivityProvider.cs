using SolmangoCLI.DecentralizedActivities;

namespace SolmangoCLI.Services;

internal interface IActivityProvider
{
    public void RegisterActivity(DecentralizedActivity activity);

    public DecentralizedActivity GetActivity(string activityId);
}