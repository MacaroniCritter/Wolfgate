using Content.Shared._WF.BoardingAction;

namespace Content.Client.Shuttles.BUI;

public sealed partial class ShuttleConsoleBoundUserInterface
{
    private void WfBoardingOpen()
    {
        if (_window != null)
            _window.WfReleaseCableRequested += () => SendMessage(new WFBoardingReleaseCableMessage());
    }
}
