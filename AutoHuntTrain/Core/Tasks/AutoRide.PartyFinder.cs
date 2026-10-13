using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Game.Ops;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// Once per ride, in a lull after arriving at the train's start, the ride puts up a Party Finder listing in The Hunt:
// many riders would rather join from the Party Finder than answer a shout, and party members share the credit on a
// mark. A flag that arrives before Recruit is pressed withdraws the attempt, so the listing never holds up the ride,
// and the next lull tries again.
internal sealed partial class AutoRide
{
    private const int ListingPollFrames = 5;

    private string listingHold = string.Empty;

    private async Task ListOnPartyFinderWhenDue()
    {
        var configuration = Plugin.Instance.Configuration;
        if (announcement is null || session.PartyFinderListingSettled || !configuration.CreatePartyFinderListing)
        {
            return;
        }

        if (PartyOps.InParty())
        {
            SettleListing("Ride: already in a party; no Party Finder listing is put up");
            return;
        }

        // A listing of the last ride may still be coming down; it is not this ride's reason to skip its own.
        var listing = Plugin.Instance.PartyFinderListing;
        if (!listing.Busy && listing.ListingUp)
        {
            SettleListing("Ride: a Party Finder listing is already up; it is left as it is");
            return;
        }

        if (listing.Refusal() is { Length: > 0 } hold)
        {
            NoteListingHold(hold);
            return;
        }

        if (!await HumanDelay(HumanAction.PartyFinder, "Ride", interrupted) || interrupted())
        {
            return;
        }

        if (PartyOps.InParty())
        {
            SettleListing("Ride: joined a party during the pause before the Party Finder listing; none is put up");
            return;
        }

        if (!listing.TryBegin(configuration.PartyFinderComment ?? string.Empty, out var refused))
        {
            NoteListingHold(refused);
            return;
        }

        listingHold = string.Empty;
        await AwaitRecruitPressed(listing);
    }

    // Past Recruit the listing finishes on its own, and ending it is the controller's business once the ride is over.
    private async Task AwaitRecruitPressed(PartyFinderListing listing)
    {
        var previous = Status;
        Status = "Putting up a Party Finder listing";
        while (listing.AwaitingRecruit && !CancelToken.IsCancellationRequested)
        {
            if (interrupted())
            {
                listing.Withdraw(conductorEnded ? "the conductor ended the train" : "a flag arrived");
                break;
            }

            await NextFrame(ListingPollFrames);
        }

        Status = previous;
        if (CancelToken.IsCancellationRequested)
        {
            listing.Withdraw("the ride was cancelled");
            return;
        }

        if (listing.Outcome == ListingOutcome.Withdrawn)
        {
            Diag("Ride: the Party Finder listing was withdrawn before Recruit; the next lull tries again");
            return;
        }

        SettleListing(listing.Outcome == ListingOutcome.Failed
            ? "Ride: the Party Finder listing could not be put up; it is not tried again on this ride"
            : "Ride: Recruit was pressed on the Party Finder listing");
    }

    private void SettleListing(string note)
    {
        session.PartyFinderListingSettled = true;
        listingHold = string.Empty;
        Diag(note);
    }

    private void NoteListingHold(string hold)
    {
        if (string.Equals(hold, listingHold, StringComparison.Ordinal))
        {
            return;
        }

        listingHold = hold;
        Diag($"Ride: the Party Finder listing waits: {hold}");
    }
}
