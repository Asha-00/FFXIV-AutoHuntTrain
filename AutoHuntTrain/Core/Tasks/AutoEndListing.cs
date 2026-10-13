using AutoHuntTrain.Core.Game;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// The first link of the chain after a ride: the Party Finder listing belongs to the train's party and world, so the
// chain lets it come down before it leaves either. The listing ends on its own steps; this only waits, for as long as
// those steps may take.
internal sealed class AutoEndListing : AutoCommon
{
    private const string Scope = "end-listing";
    private const int WaitMs = PartyFinderListing.LongestEndMs;
    private const int PollFrames = 10;

    protected override async Task Execute()
    {
        var listing = Plugin.Instance.PartyFinderListing;
        Status = "Ending the Party Finder listing";
        Diag("Waiting for the Party Finder listing to end before the rest of the chain");
        if (await WaitUntilTimed(() => !listing.Busy, WaitMs, Scope, PollFrames))
        {
            Diag("The Party Finder listing is settled; the chain goes on");
            return;
        }

        Warn($"The Party Finder listing was still being ended after {WaitMs / TimeUnits.MillisecondsPerSecond}s; the chain goes on without waiting further");
    }
}
