using Content.Client._WF.BoardingAction;
using Content.Shared._WF.BoardingAction;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleConsoleWindow
{
    private WFBoardingBanner? _wfBoardingBanner;

    /// <summary>The pilot asked to let go of the ship's hacking cable.</summary>
    public event Action? WfReleaseCableRequested;

    /// <summary>Shows hacks and disruption in the capture banner, which every helm layout already carries.</summary>
    private void WfUpdateBoarding(WFBoardingHelmState? state)
    {
        if (_wfBoardingBanner == null)
        {
            _wfBoardingBanner = new WFBoardingBanner();
            _wfBoardingBanner.ReleaseRequested += () => WfReleaseCableRequested?.Invoke();
            CaptureBanner.AddChild(_wfBoardingBanner);
        }

        _wfBoardingBanner.SetState(state);
        CaptureBanner.UpdateVisibility();
    }
}
