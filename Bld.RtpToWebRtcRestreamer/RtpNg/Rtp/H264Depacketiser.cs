#nullable enable

namespace Bld.RtpToWebRtcRestreamer.RtpNg.Rtp
{
    /// <summary>
    /// Based in https://github.com/BogdanovKirill/RtspClientSharp/blob/master/RtspClientSharp/MediaParsers/H264VideoPayloadParser.cs
    /// Distributed under MIT License
    ///
    /// @author raf.csoares@kyubinteractive.com
    /// </summary>
    public class H264Depacketiser
    {
        readonly List<KeyValuePair<int, byte[]>> _temporaryRtpPayloads = new(); // used to assemble the RTP packets that form one RTP Frame
        readonly MemoryStream _fragmentedNal = new(); // used to concatenate fragmented H264 NALs where NALs are splitted over RTP packets

        uint _previousTimestamp = 0;

        public byte[]? ProcessRtpPayload(byte[] rtpPayload, ushort seqNum, uint timestamp, int markbit, out bool isKeyFrame)
        {
            var nalUnits = ProcessRtpPayloadAsNals(rtpPayload, seqNum, timestamp, markbit, out isKeyFrame);

            if (nalUnits != null)
            {
                //Calculate total buffer size
                long totalBufferSize = 0;
                for (int i = 0; i < nalUnits.Count; i++)
                {
                    var nal = nalUnits[i];
                    long remaining = nal.Length;

                    if (remaining > 0)
                    {
                        totalBufferSize += remaining + 4; //nal + 0001
                    }
                    else
                    {
                        nalUnits.RemoveAt(i);
                        i--;
                    }
                }

                //Merge nals in same buffer using Annex-B separator (0001)
                MemoryStream data = new MemoryStream(new byte[totalBufferSize]);
                foreach (var nal in nalUnits)
                {
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(0);
                    data.WriteByte(1);
                    data.Write(nal, 0, nal.Length);
                }
                return data.ToArray();
            }
            return null;
        }

        private List<byte[]>? ProcessRtpPayloadAsNals(byte[] rtpPayload, ushort seqNum, uint timestamp, int markbit, out bool isKeyFrame)
        {
            var nalUnits = ProcessH264Payload(rtpPayload, seqNum, timestamp, markbit, out isKeyFrame);
            return nalUnits;
        }

        private List<byte[]>? ProcessH264Payload(
            byte[] rtpPayload,
            ushort seqNum,
            uint rtpTimestamp,
            int rtpMarker,
            out bool isKeyFrame)
        {
            if (_previousTimestamp != rtpTimestamp && _previousTimestamp > 0)
            {
                _temporaryRtpPayloads.Clear();
                _previousTimestamp = 0;
                _fragmentedNal.SetLength(0);
            }

            // Add to the list of payloads for the current Frame of video
            _temporaryRtpPayloads.Add(new KeyValuePair<int, byte[]>(seqNum, rtpPayload)); // TODO could optimise this and go direct to Process Frame if just 1 packet in frame
            if (rtpMarker == 1)
            {
                //Reorder to prevent UDP incorrect package order
                if (_temporaryRtpPayloads.Count > 1)
                {
                    _temporaryRtpPayloads.Sort((a, b) =>
                        (Math.Abs(b.Key - a.Key) > (0xFFFF - 2000)) ? -a.Key.CompareTo(b.Key) : a.Key.CompareTo(b.Key));
                }

                // End Marker is set. Process the list of RTP Packets (forming 1 RTP frame) and save the NALs to a file
                List<byte[]> nalUnits = ProcessH264PayloadFrame(_temporaryRtpPayloads, out isKeyFrame);
                _temporaryRtpPayloads.Clear();
                _previousTimestamp = 0;
                _fragmentedNal.SetLength(0);

                return nalUnits;
            }
            else
            {
                isKeyFrame = false;
                _previousTimestamp = rtpTimestamp;
                return null; // we don't have a frame yet. Keep accumulating RTP packets
            }
        }

        /// <summary>
        /// Process a RTP Frame. A RTP Frame can consist of several RTP Packets which have the same Timestamp
        /// Returns a list of NAL Units (with no 00 00 00 01 header and with no Size header)
        /// </summary>
        private List<byte[]> ProcessH264PayloadFrame(
            List<KeyValuePair<int, byte[]>> rtpPayloads,
            out bool isKeyFrame)
        {
            bool? isKeyFrameNullable = null;
            var nalUnits = new List<byte[]>(); // Stores the NAL units for a Video Frame. May be more than one NAL unit in a video frame.

            for (int payload_index = 0; payload_index < rtpPayloads.Count; payload_index++)
            {
                // Examine the first rtpPayload and the first byte (the NAL header)
                int nal_header_f_bit = (rtpPayloads[payload_index].Value[0] >> 7) & 0x01;
                int nal_header_nri = (rtpPayloads[payload_index].Value[0] >> 5) & 0x03;
                int nal_header_type = (rtpPayloads[payload_index].Value[0] >> 0) & 0x1F;

                // If the Nal Header Type is in the range 1..23 this is a normal NAL (not fragmented)
                // So write the NAL to the file
                if (nal_header_type >= 1 && nal_header_type <= 23)
                {
                    //norm++;
                    //Check if is Key Frame
                    CheckKeyFrame(nal_header_type, ref isKeyFrameNullable);

                    nalUnits.Add(rtpPayloads[payload_index].Value);
                }
                // There are 4 types of Aggregation Packet (split over RTP payloads)
                else if (nal_header_type == 24)
                {
                    //stap_a++;

                    // RTP packet contains multiple NALs, each with a 16 bit header
                    //   Read 16 byte size
                    //   Read NAL
                    try
                    {
                        int ptr = 1; // start after the nal_header_type which was '24'
                        // if we have at least 2 more bytes (the 16 bit size) then consume more data
                        while (ptr + 2 < (rtpPayloads[payload_index].Value.Length - 1))
                        {
                            int size = (rtpPayloads[payload_index].Value[ptr] << 8) + (rtpPayloads[payload_index].Value[ptr + 1] << 0);
                            ptr = ptr + 2;
                            byte[] nal = new byte[size];
                            Buffer.BlockCopy(rtpPayloads[payload_index].Value, ptr, nal, 0, size); // copy the NAL

                            byte reconstructed_nal_type = (byte)((nal[0] >> 0) & 0x1F);
                            //Check if is Key Frame
                            CheckKeyFrame(reconstructed_nal_type, ref isKeyFrameNullable);

                            nalUnits.Add(nal); // Add to list of NALs for this RTP frame. Start Codes like 00 00 00 01 get added later
                            ptr = ptr + size;
                        }
                    }
                    catch
                    {
                    }
                }
                else if (nal_header_type == 25)
                {
                    //stap_b++;
                }
                else if (nal_header_type == 26)
                {
                    //mtap16++;
                }
                else if (nal_header_type == 27)
                {
                    //mtap24++;
                }
                else if (nal_header_type == 28)
                {
                    //fu_a++;

                    // Parse Fragmentation Unit Header
                    int fu_indicator = rtpPayloads[payload_index].Value[0];
                    int fu_header_s = (rtpPayloads[payload_index].Value[1] >> 7) & 0x01;  // start marker
                    int fu_header_e = (rtpPayloads[payload_index].Value[1] >> 6) & 0x01;  // end marker
                    int fu_header_r = (rtpPayloads[payload_index].Value[1] >> 5) & 0x01;  // reserved. should be 0
                    int fu_header_type = (rtpPayloads[payload_index].Value[1] >> 0) & 0x1F; // Original NAL unit header

                    // Check Start and End flags
                    if (fu_header_s == 1 && fu_header_e == 0)
                    {
                        // Start of Fragment.
                        // Initialise the fragmented_nal byte array
                        // Build the NAL header with the original F and NRI flags but use the the Type field from the fu_header_type
                        byte reconstructed_nal_type = (byte)((nal_header_f_bit << 7) + (nal_header_nri << 5) + fu_header_type);

                        // Empty the stream
                        _fragmentedNal.SetLength(0);

                        // Add reconstructed_nal_type byte to the memory stream
                        _fragmentedNal.WriteByte((byte)reconstructed_nal_type);

                        // copy the rest of the RTP payload to the memory stream
                        _fragmentedNal.Write(rtpPayloads[payload_index].Value, 2, rtpPayloads[payload_index].Value.Length - 2);
                    }

                    if (fu_header_s == 0 && fu_header_e == 0)
                    {
                        // Middle part of Fragment
                        // Append this payload to the fragmented_nal
                        // Data starts after the NAL Unit Type byte and the FU Header byte
                        _fragmentedNal.Write(rtpPayloads[payload_index].Value, 2, rtpPayloads[payload_index].Value.Length - 2);
                    }

                    if (fu_header_s == 0 && fu_header_e == 1)
                    {
                        // End part of Fragment
                        // Append this payload to the fragmented_nal
                        // Data starts after the NAL Unit Type byte and the FU Header byte
                        _fragmentedNal.Write(rtpPayloads[payload_index].Value, 2, rtpPayloads[payload_index].Value.Length - 2);

                        var fragmeted_nal_array = _fragmentedNal.ToArray();
                        byte reconstructed_nal_type = (byte)((fragmeted_nal_array[0] >> 0) & 0x1F);

                        //Check if is Key Frame
                        CheckKeyFrame(reconstructed_nal_type, ref isKeyFrameNullable);

                        // Add the NAL to the array of NAL units
                        nalUnits.Add(fragmeted_nal_array);
                        _fragmentedNal.SetLength(0);
                    }
                }

                else if (nal_header_type == 29)
                {
                    //fu_b++;
                }
            }

            isKeyFrame = isKeyFrameNullable ?? false;

            // Output all the NALs that form one RTP Frame (one frame of video)
            return nalUnits;
        }

        private static void CheckKeyFrame(int nalType, ref bool? isKeyFrame)
        {
            const int sps = 7;
            const int pps = 8;
            //const int IDR_SLICE = 1;
            const int nonIdrSlice = 5;

            if (isKeyFrame == null)
            {
                isKeyFrame = nalType switch
                {
                    sps => true,
                    pps => true,
                    nonIdrSlice => false,
                    _ => null
                };
            }
            else
            {
                isKeyFrame = nalType switch
                {
                    sps => (isKeyFrame.Value ? isKeyFrame : false),
                    pps => (isKeyFrame.Value ? isKeyFrame : false),
                    nonIdrSlice => false,
                    _ => isKeyFrame
                };
            }
        }
    }
}
