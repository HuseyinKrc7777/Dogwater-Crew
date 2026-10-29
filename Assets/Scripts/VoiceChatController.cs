using System;
using Adrenak.UniMic;
using Concentus;
using Unity.Netcode;
using UnityEngine;
using static Adrenak.UniMic.Mic;
//paket lisansı
/*
Copyright (c) by various holding parties, including (but not limited to): 
Skype Limited, Xiph.Org Foundation, CSIRO, Microsoft Corporation,
Jean-Marc Valin, Gregory Maxwell, Mark Borgerding, Timothy B. Terriberry,
Logan Stromberg, Vatsal Ambastha. 
All rights are reserved by their respective holders.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of Internet Society, IETF or IETF Trust, nor the
   names of specific contributors, may be used to endorse or promote
   products derived from this software without specific prior written
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


This repository and its redistributable packages contain independently compiled
versions of the Opus C reference library, which is maintained by Xiph.org and the
Opus open-source contributors. The source code for these libraries is freely available
at https://gitlab.xiph.org/xiph/opus/-/tags/v1.5.2, and all binaries are being
redistributed to you under the same terms of the general Opus license dictated above.
*/
public class VoiceChatController : NetworkBehaviour
{

    public int resampleFrequency = 48000;

    IOpusEncoder encoder;
    IOpusDecoder decoder;
    IResampler resampler;

    byte[] encodeBuffer = new byte[4000];
    float[] resampleBuffer;
    float[] decodeBuffer;

    public StreamedAudioSource audioSource;
    Device device;

    void Start()
    {

    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Application.targetFrameRate = 60;
        Mic.Init();

        if (Mic.AvailableDevices.Count == 0)
        {
            Debug.Log("No devices");
            return;
        }

        foreach (var d in Mic.AvailableDevices)
            Debug.Log(d.Name);

        device = Mic.AvailableDevices[0];
        // f is frequency, c is channel count, s is PCM float samples
        device.OnFrameCollected += (f, c, s) =>
        {


            RefreshIfNeeded(f,c);

            string m = "Frequency: " + f;
            m += "\nChannels: " + c;
            m += "\nSample length: " + s.Length;
            var toEncode = new Span<float>();
            toEncode = s;

            if (f != resampleFrequency)
                toEncode = Resample(s,c);

            m += "\nResampled length: " + toEncode.Length;

            var encodeResult = Encode(toEncode, out Span<byte> encoded,c);
            if (encodeResult > 0)
            {
                byte[] packet = encoded.ToArray();
                VoiceFrameRpc(packet,c);
                /*
                m += "\nEncoded length: " + encoded.Length;
                m += "\nEncode ratio: " + (encoded.Length * 100f) / (toEncode.Length * 4);
                var decodeResult = Decode(encoded, out Span<float> decoded);
                if (decodeResult > 0)
                {
                    audioSource.Feed(resampleFrequency, c, decoded.ToArray());
                    m += "\nDecoded length: " + decoded.Length;
                    Debug.Log(m);
                }
                */
            }
        };
        if(!IsOwner)
            return;
        device.StartRecording(60);
    }
    [Rpc(SendTo.NotOwner, Delivery = RpcDelivery.Unreliable)]
    private void VoiceFrameRpc(byte[] frame,int c)
    {
        Span<byte> encoded = frame;
        //m += "\nEncoded length: " + encoded.Length;
        //m += "\nEncode ratio: " + (encoded.Length * 100f) / (toEncode.Length * 4);
        var decodeResult = Decode(encoded, out Span<float> decoded,c);
        if (decodeResult > 0)
        {
            audioSource.Feed(resampleFrequency, c, decoded.ToArray());
            //m += "\nDecoded length: " + decoded.Length;
            //Debug.Log(m);
        }
        // Check distance locally, then enqueue/decode/play if close enough.
    }

    void RefreshIfNeeded(int f,int c)
    {
        if (resampleBuffer == null || resampleBuffer.Length != resampleFrequency * device.FrameDurationMS * c / 1000)
            resampleBuffer = new float[resampleFrequency * device.FrameDurationMS * c / 1000];

        if (decodeBuffer == null || decodeBuffer.Length != resampleFrequency * device.FrameDurationMS * c / 1000)
            decodeBuffer = new float[resampleFrequency * device.FrameDurationMS * c / 1000];

        if (resampler == null)
        {
            resampler = ResamplerFactory.CreateResampler(device.ChannelCount, device.SamplingFrequency, resampleFrequency, 2);
        }
        else
        {
            resampler.GetRates(out int in_rate, out int out_rate);
            if (in_rate != f || device.ChannelCount != c)
            {
                resampler.Dispose();
                resampler = ResamplerFactory.CreateResampler(device.ChannelCount, device.SamplingFrequency, resampleFrequency, 5);
            }
        }

        if (encoder == null || encoder.SampleRate != resampleFrequency || encoder.NumChannels != c)
        {
            encoder?.Dispose();
            encoder = OpusCodecFactory.CreateEncoder(resampleFrequency, c, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Complexity = 3;
            encoder.Bitrate = 64000;
        }

        if (decoder == null || decoder.SampleRate != resampleFrequency || decoder.NumChannels != c)
        {
            decoder?.Dispose();
            decoder = OpusCodecFactory.CreateDecoder(resampleFrequency, c);
        }
    }

    Span<float> Resample(Span<float> samples,int c)
    {
        // Calculate input and output lengths
        int in_len = samples.Length / c; // Input samples per channel
        int out_len = resampleFrequency * device.FrameDurationMS / 1000; // Output samples per channel

        // Perform resampling into preallocated buffer
        resampler.ProcessInterleaved(samples, ref in_len, resampleBuffer, ref out_len);

        // Return only the valid portion of resampled data
        return resampleBuffer.AsSpan(0, out_len * c); // Trim to valid samples
    }

    int Encode(Span<float> toEncode, out Span<byte> encoded,int c)
    {
        int frameSize = resampleFrequency * device.FrameDurationMS / 1000; // Samples per channel
        int totalSamples = frameSize * c; // Total interleaved samples

        if (toEncode.Length < totalSamples)
        {
            Debug.LogWarning("Insufficient samples for encoding.");
            encoded = Span<byte>.Empty;
            return 0;
        }

        // Use preallocated encodeBuffer
        int result = encoder.Encode(toEncode.Slice(0, totalSamples), frameSize, encodeBuffer, encodeBuffer.Length);

        if (result > 0)
        {
            encoded = encodeBuffer.AsSpan(0, result); // Trim to actual encoded size
        }
        else
        {
            Debug.LogError("Encoding failed.");
            encoded = Span<byte>.Empty;
        }

        return result; // Return number of bytes written or 0 on failure
    }

    int Decode(Span<byte> toDecode, out Span<float> decoded,int c )
    {
        int frameSize = resampleFrequency * device.FrameDurationMS / 1000; // Samples per channel
        int maxSamples = frameSize * c; // Total interleaved samples

        // Decode the Opus packet into preallocated buffer
        int samplesPerChannel = decoder.Decode(toDecode, decodeBuffer, frameSize);

        if (samplesPerChannel > 0)
        {
            int totalSamples = samplesPerChannel * c; // Total samples across all channels
            decoded = decodeBuffer.AsSpan(0, totalSamples); // Trim to valid samples
        }
        else
        {
            Debug.LogError("Decoding failed.");
            decoded = Span<float>.Empty;
        }

        return samplesPerChannel; // Return number of samples per channel or 0 on failure
    }
}
