using Bld.WlanUtils;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Osd.Wpf.ViewModels;

public class ChannelsListViewModel : ObservableObject
{
    private WlanChannel _selectedChannel = Channels.Ch048;

    public List<WlanChannel> ChannelsList { get; } =
    [
        Channels.Ch001,
        Channels.Ch002,
        Channels.Ch003,
        Channels.Ch004,
        Channels.Ch005,
        Channels.Ch006,
        Channels.Ch007,
        Channels.Ch008,
        Channels.Ch009,
        Channels.Ch010,
        Channels.Ch011,
        Channels.Ch012,
        Channels.Ch013,
        Channels.Ch014,
        Channels.Ch032,
        Channels.Ch036,
        Channels.Ch040,
        Channels.Ch044,
        Channels.Ch048,
        Channels.Ch052,
        Channels.Ch056,
        Channels.Ch060,
        Channels.Ch064,
        Channels.Ch068,
        Channels.Ch072,
        Channels.Ch076,
        Channels.Ch080,
        Channels.Ch084,
        Channels.Ch088,
        Channels.Ch092,
        Channels.Ch096,
        Channels.Ch100,
        Channels.Ch104,
        Channels.Ch108,
        Channels.Ch112,
        Channels.Ch116,
        Channels.Ch120,
        Channels.Ch124,
        Channels.Ch128,
        Channels.Ch132,
        Channels.Ch136,
        Channels.Ch140,
        Channels.Ch144,
        Channels.Ch149,
        Channels.Ch153,
        Channels.Ch157,
        Channels.Ch161,
        Channels.Ch165,
        Channels.Ch169,
        Channels.Ch173,
        Channels.Ch177
    ];

    public WlanChannel SelectedChannel
    {
        get => _selectedChannel;
        set => SetProperty(ref _selectedChannel, value);
    }
}