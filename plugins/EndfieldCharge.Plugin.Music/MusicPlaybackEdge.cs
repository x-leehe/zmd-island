namespace EndfieldCharge.Host.Plugins.Music;

/// <summary>Tracks actual playback transitions, ignoring temporary SMTC empty/error frames.</summary>
public sealed class MusicPlaybackEdge
{
    private bool _hasBaseline;
    private bool _wasPlaying;

    public bool Observe(string? sourceAppId, bool isPlaying)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
            return false;

        if (!_hasBaseline)
        {
            _hasBaseline = true;
            _wasPlaying = isPlaying;
            return false;
        }

        bool started = isPlaying && !_wasPlaying;
        _wasPlaying = isPlaying;
        return started;
    }
}
