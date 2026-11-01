using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Inspection
{
    public class InspectionApi : MonoBehaviour
    {
        [Tooltip("Unity 和 React 必须连接同一 Go 服务")]
        public string baseUrl = "http://localhost:8080";
        public int timeoutSeconds = 10;

        public IEnumerator List(Action<Issue[]> success, Action<string> failure)
        {
            using (var request = UnityWebRequest.Get(baseUrl.TrimEnd('/') + "/api/issues"))
            {
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { failure(Error(request)); yield break; }
                IssueListResponse body = null;
                try { body = JsonUtility.FromJson<IssueListResponse>(request.downloadHandler.text); }
                catch (Exception) { /* Report malformed payload below. */ }
                if (body == null || body.data == null) failure("服务返回的问题列表格式不正确。");
                else success(body.data);
            }
        }

        public IEnumerator Create(string json, string key, Action<Issue> success, Action<string> failure)
        {
            using (var request = new UnityWebRequest(baseUrl.TrimEnd('/') + "/api/issues", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Idempotency-Key", key);
                request.timeout = timeoutSeconds;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) { failure(Error(request)); yield break; }
                IssueResponse body = null;
                try { body = JsonUtility.FromJson<IssueResponse>(request.downloadHandler.text); }
                catch (Exception) { /* Report malformed payload below. */ }
                if (body == null || body.data == null || string.IsNullOrEmpty(body.data.id) || body.data.position == null)
                    failure("保存响应格式不正确，请重试或刷新确认。");
                else success(body.data);
            }
        }

        static string Error(UnityWebRequest request)
        {
            try
            {
                var body = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(body?.error?.message)) return body.error.message;
            }
            catch (Exception) { }
            return request.responseCode == 0
                ? "无法连接服务或请求超时。请确认 Go 后端已启动，再点击重试。"
                : "请求失败（HTTP " + request.responseCode + "），请稍后重试。";
        }
    }
}
