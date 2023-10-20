using CommunityToolkit.Mvvm.ComponentModel;

namespace OsdDemo.ViewModels;

public class MavlinkStatViewModel : ObservableObject
{
    private int _count;

    public required string Name { get; init; }

    public int Count
    {
        get => _count;
        set => SetProperty(ref _count, value);
    }
}