using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// A link of the chain after a ride: the train's party is left before the way home, since a party is no use
// once the train is over and the game refuses a data center transfer while in one.
internal sealed class AutoLeaveParty : AutoCommon
{
    private const string Scope = "leave-party";

    protected override async Task Execute()
    {
        Diag("Leaving the train's party now that the ride is over");
        await LeaveParty(Scope);
    }
}
