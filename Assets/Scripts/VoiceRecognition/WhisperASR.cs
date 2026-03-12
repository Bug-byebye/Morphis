using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Morphis.VoiceRecognition
{
    /// <summary>
    /// Hugging Face Whisper API 语音识别
    /// 支持中文和英文语音识别
    /// </summary>
    public class WhisperASR : MonoBehaviour
    {
        [Header("API Settings")]
        [SerializeField] private string apiToken = ""; // Hugging Face API Token
        [SerializeField] private string modelId = "openai/whisper-large-v3"; // 或 whisper-medium, whisper-small
        [SerializeField] private string inferenceBaseUrl = "https://router.huggingface.co/hf-inference/models";
        [SerializeField] private string fallbackInferenceBaseUrl = "https://api-inference.huggingface.co/models";
        [SerializeField] private int requestTimeoutSeconds = 60;
        
        [Header("Recording Settings")]
        [SerializeField] private int recordingLength = 10; // 最大录音时长（秒）
        [SerializeField] private int sampleRate = 16000; // Whisper推荐16kHz
        
        private AudioClip recordingClip;
        private bool isRecording = false;
        private string microphoneDevice;
        
        public bool IsRecording => isRecording;

        /// <summary>
        /// Set API token at runtime (used by companion chat bootstrap).
        /// </summary>
        public void SetApiToken(string token)
        {
            apiToken = token == null ? string.Empty : token.Trim();
        }
        
        private void Start()
        {
            // 获取麦克风设备
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
                Debug.Log($"[WhisperASR] 使用麦克风: {microphoneDevice}");
            }
            else
            {
                Debug.LogError("[WhisperASR] 未检测到麦克风设备！");
            }
        }
        
        /// <summary>
        /// 开始录音
        /// </summary>
        public void StartRecording()
        {
            if (isRecording)
            {
                Debug.LogWarning("[WhisperASR] 已经在录音中");
                return;
            }
            
            if (string.IsNullOrEmpty(microphoneDevice))
            {
                Debug.LogError("[WhisperASR] 没有可用的麦克风设备");
                return;
            }
            
            Debug.Log("[WhisperASR] 🎤 开始录音...");
            recordingClip = Microphone.Start(microphoneDevice, false, recordingLength, sampleRate);
            isRecording = true;
        }
        
        /// <summary>
        /// 停止录音并发送到Whisper API识别
        /// </summary>
        public void StopRecordingAndRecognize(Action<string> onSuccess, Action<string> onError)
        {
            if (!isRecording)
            {
                Debug.LogWarning("[WhisperASR] 没有在录音");
                return;
            }
            
            Debug.Log("[WhisperASR] ⏹️ 停止录音");
            
            int position = Microphone.GetPosition(microphoneDevice);
            Microphone.End(microphoneDevice);
            isRecording = false;
            
            if (position == 0)
            {
                onError?.Invoke("录音失败，请重试");
                return;
            }
            
            // 裁剪录音到实际长度
            float[] samples = new float[position * recordingClip.channels];
            recordingClip.GetData(samples, 0);
            
            AudioClip trimmedClip = AudioClip.Create("Recording", position, recordingClip.channels, 
                recordingClip.frequency, false);
            trimmedClip.SetData(samples, 0);
            
            // 转换为WAV格式并发送
            StartCoroutine(TranscribeAudio(trimmedClip, onSuccess, onError));
        }
        
        /// <summary>
        /// 发送音频到Whisper API进行识别
        /// </summary>
        private IEnumerator TranscribeAudio(AudioClip clip, Action<string> onSuccess, Action<string> onError)
        {
            Debug.Log("[WhisperASR] 🔄 正在识别语音...");
            
            // 检查API Token
            if (string.IsNullOrEmpty(apiToken))
            {
                onError?.Invoke("请设置Hugging Face API Token");
                yield break;
            }
            
            // 转换AudioClip为WAV字节
            byte[] wavData = ConvertAudioClipToWav(clip);
            
            // 构建API URL
            string baseUrl = string.IsNullOrWhiteSpace(inferenceBaseUrl)
                ? "https://router.huggingface.co/hf-inference/models"
                : inferenceBaseUrl.TrimEnd('/');
            string apiUrl = $"{baseUrl}/{modelId}";

            bool success = false;
            string responseText = "";
            long statusCode = 0;
            string requestError = "";

            yield return SendTranscriptionRequest(
                apiUrl,
                wavData,
                (reqSuccess, reqResponseText, reqStatusCode, reqError) =>
                {
                    success = reqSuccess;
                    responseText = reqResponseText;
                    statusCode = reqStatusCode;
                    requestError = reqError;
                });

            // Some HF tokens do not have router "Inference Providers" permission.
            // Retry with legacy endpoint once when this specific auth error appears.
            if (!success && ShouldRetryWithFallback(statusCode, responseText))
            {
                string fallbackBaseUrl = string.IsNullOrWhiteSpace(fallbackInferenceBaseUrl)
                    ? "https://api-inference.huggingface.co/models"
                    : fallbackInferenceBaseUrl.TrimEnd('/');
                string fallbackUrl = $"{fallbackBaseUrl}/{modelId}";

                Debug.LogWarning("[WhisperASR] Router auth failed, retrying with fallback inference endpoint.");
                yield return SendTranscriptionRequest(
                    fallbackUrl,
                    wavData,
                    (reqSuccess, reqResponseText, reqStatusCode, reqError) =>
                    {
                        success = reqSuccess;
                        responseText = reqResponseText;
                        statusCode = reqStatusCode;
                        requestError = reqError;
                    });
            }

            if (success)
            {
                Debug.Log($"[WhisperASR] API Response: {responseText}");

                try
                {
                    string recognizedText = ParseRecognizedText(responseText);

                    if (!string.IsNullOrEmpty(recognizedText))
                    {
                        Debug.Log($"[WhisperASR] ✅ 识别结果: {recognizedText}");
                        onSuccess?.Invoke(recognizedText.Trim());
                    }
                    else
                    {
                        onError?.Invoke("识别结果为空");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[WhisperASR] JSON解析错误: {e.Message}");
                    onError?.Invoke("解析识别结果失败");
                }
                yield break;
            }

            string error = BuildDetailedError(requestError, statusCode, responseText);
            Debug.LogError($"[WhisperASR] {error}");
            onError?.Invoke(error);
        }

        private string BuildDetailedError(string requestError, long statusCode, string responseBody)
        {
            string body = string.IsNullOrWhiteSpace(responseBody) ? "(empty)" : responseBody.Trim();
            string baseMessage = $"API请求失败: {requestError} (HTTP {statusCode})";

            if (statusCode == 401 || statusCode == 403)
            {
                if (ContainsInferenceProvidersPermissionError(body))
                    return $"{baseMessage}。当前HF Token缺少“Inference Providers”权限，请到 Hugging Face 设置里更新 Token 权限后重试。服务端返回: {TruncateForUi(body)}";
                return $"{baseMessage}。请检查HF Token是否有效。服务端返回: {TruncateForUi(body)}";
            }

            if (statusCode == 429)
                return $"{baseMessage}。请求过于频繁或额度受限，请稍后重试。服务端返回: {TruncateForUi(body)}";

            if (statusCode == 503)
                return $"{baseMessage}。模型可能在冷启动，请等待10-30秒后重试。服务端返回: {TruncateForUi(body)}";
            
            if (statusCode == 410)
                return $"{baseMessage}。接口地址已弃用，请使用 router.huggingface.co/hf-inference。服务端返回: {TruncateForUi(body)}";

            if (statusCode == 0)
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                    return $"{baseMessage}。当前设备无网络连接。";
                return $"{baseMessage}。通常是网络/证书/DNS问题，或目标域名不可达。服务端返回: {TruncateForUi(body)}";
            }

            return $"{baseMessage}。服务端返回: {TruncateForUi(body)}";
        }

        private IEnumerator SendTranscriptionRequest(
            string apiUrl,
            byte[] wavData,
            Action<bool, string, long, string> onComplete)
        {
            using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(wavData);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Authorization", $"Bearer {apiToken}");
                request.SetRequestHeader("Content-Type", "audio/wav");
                request.SetRequestHeader("Accept", "application/json");
                request.timeout = Mathf.Max(10, requestTimeoutSeconds);

                yield return request.SendWebRequest();

                string responseText = request.downloadHandler != null ? request.downloadHandler.text : "";
                long statusCode = request.responseCode;
                bool success = request.result == UnityWebRequest.Result.Success;
                onComplete?.Invoke(success, responseText, statusCode, request.error);
            }
        }

        private bool ShouldRetryWithFallback(long statusCode, string responseBody)
        {
            if (statusCode != 401 && statusCode != 403)
                return false;

            return ContainsInferenceProvidersPermissionError(responseBody);
        }

        private bool ContainsInferenceProvidersPermissionError(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return false;

            return responseBody.IndexOf("Inference Providers", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string TruncateForUi(string text, int maxLength = 220)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;

            return text.Substring(0, maxLength) + "...";
        }
        
        private string ParseRecognizedText(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return string.Empty;
            
            string trimmed = responseText.Trim();
            
            // 情况1: 返回纯字符串，例如 "你好"
            if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
                return trimmed.Trim('"');
            
            // 情况2: 返回对象，例如 {"text":"你好"}
            var response = JsonUtility.FromJson<WhisperResponse>(trimmed);
            if (response != null && !string.IsNullOrEmpty(response.text))
                return response.text;
            
            // 兜底：从JSON文本中提取 text 字段
            Match match = Regex.Match(trimmed, "\"text\"\\s*:\\s*\"(?<value>.*?)\"");
            if (match.Success)
                return match.Groups["value"].Value
                    .Replace("\\n", "\n")
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\");
            
            return string.Empty;
        }
        
        /// <summary>
        /// 将AudioClip转换为WAV格式字节数组
        /// </summary>
        private byte[] ConvertAudioClipToWav(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            
            short[] intData = new short[samples.Length];
            byte[] bytesData = new byte[samples.Length * 2];
            
            int rescaleFactor = 32767;
            
            for (int i = 0; i < samples.Length; i++)
            {
                intData[i] = (short)(samples[i] * rescaleFactor);
                byte[] byteArr = BitConverter.GetBytes(intData[i]);
                byteArr.CopyTo(bytesData, i * 2);
            }
            
            byte[] wav = new byte[44 + bytesData.Length];
            
            // WAV文件头
            int sampleRate = clip.frequency;
            int channels = clip.channels;
            int bitsPerSample = 16;
            
            // RIFF header
            System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
            BitConverter.GetBytes(36 + bytesData.Length).CopyTo(wav, 4);
            System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
            
            // fmt chunk
            System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
            BitConverter.GetBytes(16).CopyTo(wav, 16); // Subchunk1Size
            BitConverter.GetBytes((short)1).CopyTo(wav, 20); // AudioFormat (PCM)
            BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
            BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
            BitConverter.GetBytes(sampleRate * channels * bitsPerSample / 8).CopyTo(wav, 28); // ByteRate
            BitConverter.GetBytes((short)(channels * bitsPerSample / 8)).CopyTo(wav, 32); // BlockAlign
            BitConverter.GetBytes((short)bitsPerSample).CopyTo(wav, 34);
            
            // data chunk
            System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
            BitConverter.GetBytes(bytesData.Length).CopyTo(wav, 40);
            bytesData.CopyTo(wav, 44);
            
            return wav;
        }
        
        [Serializable]
        private class WhisperResponse
        {
            public string text;
        }
    }
}
