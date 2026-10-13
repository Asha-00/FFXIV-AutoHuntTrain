using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// An instance switch run as its own task, so the ride can cut it short the moment a newer flag arrives.
internal sealed class InstanceSwitch(int instance, string label) : AutoCommon
{
    public bool Switched { get; private set; }

    // The task runner swallows exceptions, so a switch that blew up would otherwise look like a quiet failure.
    public Exception? Fault { get; private set; }

    protected override async Task Execute()
    {
        try
        {
            Switched = await SwitchToInstance(instance, label);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Fault = exception;
        }
    }
}
