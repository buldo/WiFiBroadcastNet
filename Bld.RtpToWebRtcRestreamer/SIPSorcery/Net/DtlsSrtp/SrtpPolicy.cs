//-----------------------------------------------------------------------------
// Filename: SrtpPolicy.cs
//
// Description: SRTP Policy encapsulation.
//
// Derived From: https://github.com/jitsi/jitsi-srtp/blob/master/src/main/java/org/jitsi/srtp/SrtpPolicy.java
//
// Author(s):
// Rafael Soares (raf.csoares@kyubinteractive.com)
//
// History:
// 01 Jul 2020	Rafael Soares   Created.
//
// License:
// Customisations: BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
// Original Source: Apache License: see below
//-----------------------------------------------------------------------------

/*
 * Copyright @ 2015 - present 8x8, Inc
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

namespace Bld.RtpToWebRtcRestreamer.SIPSorcery.Net.DtlsSrtp;

/// <summary>
/// SrtpPolicy holds the SRTP encryption / authentication policy of a SRTP
/// session.
///
/// @author Bing SU (nova.su @gmail.com)
/// </summary>
internal class SrtpPolicy
{
    public const int NullEncryption = 0;
    public const int AescmEncryption = 1;
    public const int TwofishEncryption = 3;
    public const int Aesf8Encryption = 2;
    public const int Twofishf8Encryption = 4;
    public const int NullAuthentication = 0;
    public const int Hmacsha1Authentication = 1;
    public const int SkeinAuthentication = 2;

    public int AuthKeyLength { get; }

    public int AuthTagLength { get; }

    public int AuthType { get; }

    public int EncKeyLength { get; }

    public int EncType { get; }

    public int SaltKeyLength { get; }

    /**
         * Construct a SRTPPolicy object based on given parameters.
         * This class acts as a storage class, so all the parameters are passed in
         * through this constructor.
         *
         * @param encType SRTP encryption type
         * @param encKeyLength SRTP encryption key length
         * @param authType SRTP authentication type
         * @param authKeyLength SRTP authentication key length
         * @param authTagLength SRTP authentication tag length
         * @param saltKeyLength SRTP salt key length
         */
    public SrtpPolicy(int encType,
        int encKeyLength,
        int authType,
        int authKeyLength,
        int authTagLength,
        int saltKeyLength)
    {
        EncType = encType;
        EncKeyLength = encKeyLength;
        AuthType = authType;
        AuthKeyLength = authKeyLength;
        AuthTagLength = authTagLength;
        SaltKeyLength = saltKeyLength;
    }
}