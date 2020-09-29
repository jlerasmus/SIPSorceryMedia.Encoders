﻿//-----------------------------------------------------------------------------
// Filename: VideoEncoderEndPoint.cs
//
// Description: Implements a video source and sink that is for encode/decode
// only, i.e. no hooks for system audio or video devices.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
//
// History:
// 20 Aug 2020  Aaron Clauson	Created, Dublin, Ireland.
// 29 Sep 2020  Aaron Clauson   Moved from SIPSorceryMedia.Windows assembly into
//                              a new dedicated project for x-platform support.
//
// License: 
// BSD 3-Clause "New" or "Revised" License, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SIPSorceryMedia.Abstractions.V1;
using SIPSorceryMedia.Encoders.Codecs;
using SIPSorceryMedia.Abstractions;

namespace SIPSorceryMedia.Encoders
{
    public class VideoEncoderEndPoint : IVideoSource, IVideoSink, IDisposable
    {
        private const int VIDEO_SAMPLING_RATE = 90000;
        private const int DEFAULT_FRAMES_PER_SECOND = 30;

        private ILogger logger = SIPSorcery.LogFactory.CreateLogger<VideoEncoderEndPoint>();

        public static readonly List<VideoCodecsEnum> SupportedCodecs = new List<VideoCodecsEnum>
        {
            VideoCodecsEnum.VP8
        };

        private CodecManager<VideoCodecsEnum> _codecManager;
        private VideoEncoder _videoEncoder;
        private bool _isClosed;

        /// <summary>
        /// This video source DOES NOT generate raw samples. Subscribe to the encoded samples event
        /// to get samples ready for passing to the RTP transport layer.
        /// </summary>
        [Obsolete("This video source only generates encoded samples. No raw video samples will be supplied to this event.")]
        public event RawVideoSampleDelegate OnVideoSourceRawSample { add { } remove { } }

        /// <summary>
        /// This event will be fired whenever a video sample is encoded and is ready to transmit to the remote party.
        /// </summary>
        public event EncodedSampleDelegate OnVideoSourceEncodedSample;

        /// <summary>
        /// This event is fired after the sink decodes a video frame from the remote party.
        /// </summary>
        public event VideoSinkSampleDecodedDelegate OnVideoSinkDecodedSample;

#pragma warning disable CS0067
        public event SourceErrorDelegate OnVideoSourceError;
#pragma warning restore CS0067

        /// <summary>
        /// Creates a new video source that can encode and decode samples.
        /// </summary>
        public VideoEncoderEndPoint()
        {
            _codecManager = new CodecManager<VideoCodecsEnum>(SupportedCodecs);
            _videoEncoder = new VideoEncoder();
        }

        public void RestrictCodecs(List<VideoCodecsEnum> codecs) => _codecManager.RestrictCodecs(codecs);
        public List<VideoCodecsEnum> GetVideoSourceFormats() => _codecManager.GetSourceFormats();
        public void SetVideoSourceFormat(VideoCodecsEnum videoFormat) => _codecManager.SetSelectedCodec(videoFormat);
        public List<VideoCodecsEnum> GetVideoSinkFormats() => _codecManager.GetSourceFormats();
        public void SetVideoSinkFormat(VideoCodecsEnum videoFormat) => _codecManager.SetSelectedCodec(videoFormat);

        public void ForceKeyFrame() => _videoEncoder.ForceKeyFrame();
        public void GotVideoRtp(IPEndPoint remoteEndPoint, uint ssrc, uint seqnum, uint timestamp, int payloadID, bool marker, byte[] payload) =>
            throw new ApplicationException("The Windows Video End Point requires full video frames rather than individual RTP packets.");
        public bool HasEncodedVideoSubscribers() => OnVideoSourceEncodedSample != null;
        public bool IsVideoSourcePaused() => false;
        public Task PauseVideo() => Task.CompletedTask;
        public Task ResumeVideo() => Task.CompletedTask;
        public Task StartVideo() => Task.CompletedTask;

        public MediaEndPoints ToMediaEndPoints()
        {
            return new MediaEndPoints
            {
                VideoSource = this,
                VideoSink = this
            };
        }

        public void ExternalVideoSourceRawSample(uint durationMilliseconds, int width, int height, byte[] sample, VideoPixelFormatsEnum pixelFormat)
        {
            if (!_isClosed)
            {


                //lock (_encoderLock)
                //{
                //    if (_vp8Encoder == null)
                //    {
                //        _vp8Encoder = new Vp8Codec();
                //        _vp8Encoder.InitialiseEncoder((uint)width, (uint)height);
                //    }

                if (OnVideoSourceEncodedSample != null)
                {
                    //byte[] i420Buffer = null;

                    //switch(pixelFormat)
                    //{
                    //    case VideoPixelFormatsEnum.Bgra:
                    //        i420Buffer = PixelConverter.RGBAtoI420(sample, width, height);
                    //        break;
                    //    case VideoPixelFormatsEnum.Bgr:
                    //        i420Buffer = PixelConverter.BGRtoI420(sample, width, height);
                    //        break;
                    //    default:
                    //        i420Buffer = PixelConverter.RGBtoI420(sample, width, height);
                    //        break;
                    //}

                    //var encodedBuffer = _vp8Encoder.Encode(i420Buffer, _forceKeyFrame);
                    var encodedBuffer = _videoEncoder.EncodeVideo(width, height, sample, pixelFormat, VideoCodecsEnum.VP8);

                    //SetBitmapData(sample, _encodeBmp, pixelFormat);

                    //var nv12bmp = SoftwareBitmap.Convert(_encodeBmp, BitmapPixelFormat.Nv12);
                    //byte[] nv12Buffer = null;

                    //using (BitmapBuffer buffer = nv12bmp.LockBuffer(BitmapBufferAccessMode.Read))
                    //{
                    //    using (var reference = buffer.CreateReference())
                    //    {
                    //        unsafe
                    //        {
                    //            byte* dataInBytes;
                    //            uint capacity;
                    //            ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

                    //            nv12Buffer = new byte[capacity];
                    //            Marshal.Copy((IntPtr)dataInBytes, nv12Buffer, 0, (int)capacity);
                    //        }
                    //    }
                    //}

                    //byte[] encodedBuffer = _vp8Encoder.Encode(nv12Buffer, _forceKeyFrame);

                    if (encodedBuffer != null)
                    {
                        //Console.WriteLine($"encoded buffer: {encodedBuffer.HexStr()}");
                        uint fps = (durationMilliseconds > 0) ? 1000 / durationMilliseconds : DEFAULT_FRAMES_PER_SECOND;
                        uint durationRtpTS = VIDEO_SAMPLING_RATE / fps;
                        OnVideoSourceEncodedSample.Invoke(durationRtpTS, encodedBuffer);
                    }

                    //if (_forceKeyFrame)
                    //{
                    //    _forceKeyFrame = false;
                    //}
                }
                //}
            }
        }

        public void GotVideoFrame(IPEndPoint remoteEndPoint, uint timestamp, byte[] frame)
        {
            if (!_isClosed)
            {
                //lock (_decoderLock)
                //{
                //    if (_vp8Decoder == null)
                //    {
                //        _vp8Decoder = new Vp8Codec();
                //        _vp8Decoder.InitialiseDecoder();
                //        //DateTime startTime = DateTime.Now;
                //    }

                    //List<byte[]> decodedFrames = _vp8Decoder.Decode(frame, frame.Length, out var width, out var height);

                    //if (decodedFrames == null)
                    //{
                    //    logger.LogWarning("VPX decode of video sample failed.");
                    //}
                    //else
                    //{
                        foreach (var decoded in _videoEncoder.DecodeVideo(frame, VideoPixelFormatsEnum.Bgr, VideoCodecsEnum.VP8))
                        {
                            //byte[] rgb = PixelConverter.I420toBGR(decodedFrame, (int)decodedFrame.Width, (int)decodedFrame.Height);
                            //Console.WriteLine($"VP8 decode took {DateTime.Now.Subtract(startTime).TotalMilliseconds}ms.");
                            OnVideoSinkDecodedSample(decoded.Sample, decoded.Width, decoded.Height, (int)(decoded.Width * 3), VideoPixelFormatsEnum.Bgr);
                        }
                   // }
                }
           //}
        }

        public Task CloseVideo()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                Dispose();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _videoEncoder?.Dispose();
        }
    }
}