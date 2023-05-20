namespace WiFiBroadcastNet;


public class UeventDescription
{
    public string DevType { get; private set; }
    
    public string Driver { get; private set; }
    
    public string Product { get; private set; }
    
    public string Type { get; private set; }
    
    public string Interface { get; private set; }
    
    public string ModAlias { get; private set; }

    /// <remarks>
    /// ExampleContent:
    /// DEVTYPE=usb_interface
    /// DRIVER=rtl88xxau_wfb
    /// PRODUCT=2604/12/0
    /// TYPE=0/0/0
    /// INTERFACE=255/255/255
    /// MODALIAS=usb:v2604p0012d0000dc00dsc00dp00icFFiscFFipFFin00
    /// </remarks>
    public static UeventDescription Parse(string[] lines)
    {
        var newDescription = new UeventDescription();
        var filteredLines = lines
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l));
        foreach (var line in filteredLines)
        {
            var lineParts = line.Split('=');
            var value = lineParts[1];
            switch (lineParts[0])
            {
                case "DEVTYPE":
                    newDescription.DevType = value;
                    break;
                case "DRIVER":
                    newDescription.Driver = value;
                    break;
                case "PRODUCT":
                    newDescription.Product = value;
                    break;
                case "TYPE":
                    newDescription.Type = value;
                    break;
                case "INTERFACE":
                    newDescription.Interface = value;
                    break;
                case "MODALIAS":
                    newDescription.ModAlias = value;
                    break;
            }
        }

        return newDescription;
    }
}