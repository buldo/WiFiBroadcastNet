using Bld.WlanUtils;

namespace CommonAbstractions;

public interface IWfbHost
{
    int GetDevicesCount();

    void Start(WlanChannel startChannel);
    void SetChannel(WlanChannel channel);
    void Stop();
}