using Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.WebRTC;

/// <summary>
/// Represents a fingerprint of a certificate used to authenticate WebRTC communications.
/// </summary>
internal class RTCDtlsFingerprint
{
    /// <summary>
    /// One of the hash function algorithms defined in the 'Hash function Textual Names' registry.
    /// </summary>
    public string algorithm;

    /// <summary>
    /// The value of the certificate fingerprint in lower-case hex string as expressed utilising 
    /// the syntax of 'fingerprint' in [RFC4572] Section 5.
    /// </summary>
    public string value;

    public override string ToString()
    {
        // FireFox wasn't happy unless the fingerprint hash was in upper case.
        return $"{algorithm} {value.ToUpper()}";
    }

    /// <summary>
    /// Attempts to parse the fingerprint fields from a string.
    /// </summary>
    /// <param name="str">The string to parse from.</param>
    /// <param name="fingerprint">If successful a fingerprint object.</param>
    /// <returns>True if a fingerprint was successfully parsed. False if not.</returns>
    public static bool TryParse(string str, out RTCDtlsFingerprint fingerprint)
    {
        fingerprint = null;

        if (string.IsNullOrEmpty(str))
        {
            return false;
        }

        var spaceIndex = str.IndexOf(' ');
        if (spaceIndex == -1)
        {
            return false;
        }

        var algStr = str.Substring(0, spaceIndex);
        var val = str.Substring(spaceIndex + 1);

        if (!DtlsUtils.IsHashSupported(algStr))
        {
            return false;
        }

        fingerprint = new RTCDtlsFingerprint
        {
            algorithm = algStr,
            value = val
        };
        return true;
    }
}