using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace UnityEngine.Networking
{
    public class UploadHandler { internal byte[] data; }
    public sealed class UploadHandlerRaw : UploadHandler { public UploadHandlerRaw(byte[] data) { this.data = data; } }
    public class DownloadHandler
    {
        public byte[] data { get; internal set; } = Array.Empty<byte>();
        public string text => System.Text.Encoding.UTF8.GetString(data ?? Array.Empty<byte>());
    }
    public sealed class DownloadHandlerBuffer : DownloadHandler { }
    public sealed class UnityWebRequestAsyncOperation : AsyncOperation { }

    // HttpClient-backed stand-in, enough for the allocation agent's JSON RPCs.
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        private static readonly HttpClient http = new HttpClient();
        private readonly string url, method;
        private readonly System.Collections.Generic.Dictionary<string, string> headers = new System.Collections.Generic.Dictionary<string, string>();
        public UploadHandler uploadHandler { get; set; }
        public DownloadHandler downloadHandler { get; set; }
        public int timeout { get; set; }
        public long responseCode { get; private set; }
        public Result result { get; private set; } = Result.InProgress;
        public UnityWebRequest(string url, string method) { this.url = url; this.method = method; }
        public void SetRequestHeader(string name, string value) => headers[name] = value;

        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            var operation = new UnityWebRequestAsyncOperation();
            _ = Run(operation);
            return operation;
        }

        private async Task Run(UnityWebRequestAsyncOperation operation)
        {
            try
            {
                using (var message = new HttpRequestMessage(new HttpMethod(method), url))
                using (var cancel = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeout > 0 ? timeout : 30)))
                {
                    if (uploadHandler?.data != null)
                    {
                        message.Content = new ByteArrayContent(uploadHandler.data);
                        if (headers.TryGetValue("Content-Type", out var type)) message.Content.Headers.TryAddWithoutValidation("Content-Type", type);
                    }
                    var response = await http.SendAsync(message, cancel.Token).ConfigureAwait(false);
                    responseCode = (long)response.StatusCode;
                    var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (downloadHandler != null) downloadHandler.data = body;
                    result = response.IsSuccessStatusCode ? Result.Success : Result.ProtocolError;
                }
            }
            catch (Exception) { result = Result.ConnectionError; }
            finally { operation.isDone = true; }
        }

        public void Dispose() { }
    }
}
