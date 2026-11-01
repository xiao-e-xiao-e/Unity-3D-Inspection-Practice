using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inspection
{
    public class InspectionController : MonoBehaviour
    {
        public Camera viewCamera;
        public InspectionApi api;
        public LayerMask inspectableLayers = 1 << 8;
        public float maxDistance = 30f;
        enum Mode { Browsing, Editing, Submitting, Viewing }
        Mode mode;
        InspectionUI ui;
        GameObject draftMarker;
        Vector3 hitPosition;
        string requestKey;
        string frozenJson;
        int ignoreClicksThroughFrame;
        bool loading;
        int detailRequest;
        const int MarkerLayer = 9;
        readonly Dictionary<string, GameObject> savedMarkers = new Dictionary<string, GameObject>();
        Material draftMaterial, openMaterial, progressMaterial, resolvedMaterial;
        public bool IsEditing => mode != Mode.Browsing;
        public int SavedMarkerCount => savedMarkers.Count;
        public bool HasDraftMarker => draftMarker != null;

        void Start()
        {
            draftMaterial = MarkerMaterial(new Color(1f, .75f, .15f));
            openMaterial = MarkerMaterial(new Color(.95f, .28f, .2f));
            progressMaterial = MarkerMaterial(new Color(.2f, .65f, 1f));
            resolvedMaterial = MarkerMaterial(new Color(.2f, .8f, .45f));
            ui = new InspectionUI(Submit, Cancel, Refresh, CloseDetails);
            Refresh();
        }

        // LateUpdate runs after UI events; the frame guard also protects the click that closes a form.
        void LateUpdate()
        {
            if (mode == Mode.Editing && Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }
            if (mode == Mode.Viewing && Input.GetKeyDown(KeyCode.Escape)) { CloseDetails(); return; }
            if (mode != Mode.Browsing || loading || Time.frameCount <= ignoreClicksThroughFrame || !Input.GetMouseButtonDown(0) || Input.GetMouseButton(1)) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            // Nearest hit wins, so markers behind walls cannot be selected through geometry.
            if (!Physics.Raycast(viewCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, maxDistance, inspectableLayers.value | (1 << MarkerLayer), QueryTriggerInteraction.Collide)) return;
            var selected = hit.collider.GetComponent<IssueMarker>();
            if (selected != null) { OpenDetails(selected.issueId); return; }
            hitPosition = hit.point;
            draftMarker = Marker("Pending issue", hit.point + hit.normal * .055f, draftMaterial);
            requestKey = Guid.NewGuid().ToString();
            frozenJson = null;
            mode = Mode.Editing;
            ui.Open(hitPosition);
        }

        void Submit()
        {
            if (mode != Mode.Editing) return;
            if (frozenJson == null)
            {
                string title = ui.Title.Trim();
                if (title.Length == 0) { ui.FormMessage("标题不能为空。", true); return; }
                frozenJson = JsonUtility.ToJson(new CreateIssue { title = title, description = ui.Description, priority = ui.Priority, position = new PositionData(hitPosition) });
            }
            mode = Mode.Submitting;
            ui.SetSubmitting(true, true);
            ui.FormMessage("正在提交，请稍候…", false);
            StartCoroutine(api.Create(frozenJson, requestKey, OnSaved, OnFailed));
        }

        void OnSaved(Issue issue)
        {
            draftMarker.name = "Issue " + issue.id;
            draftMarker.GetComponent<Renderer>().sharedMaterial = MaterialFor(issue.status);
            MakeSelectable(draftMarker, issue.id);
            if (savedMarkers.TryGetValue(issue.id, out var existing)) Destroy(existing);
            savedMarkers[issue.id] = draftMarker;
            draftMarker = null;
            CloseForm();
            ui.Notice("提交成功：" + issue.title + "   |   已保存标记 " + savedMarkers.Count, false);
        }

        void OnFailed(string message)
        {
            mode = Mode.Editing;
            // Retry the identical snapshot/key: a timed-out request may already have committed.
            ui.SetSubmitting(false, true);
            ui.FormMessage(message + "\n重试沿用首次提交内容。取消后可重新填写；若网络中断，请先刷新确认是否已保存。", true);
        }

        void Cancel()
        {
            if (mode != Mode.Editing) return;
            Destroy(draftMarker);
            draftMarker = null;
            CloseForm();
            ui.Notice("已取消填写，未保存的本地标记已移除。", false);
        }

        void CloseForm()
        {
            mode = Mode.Browsing;
            frozenJson = null;
            ignoreClicksThroughFrame = Time.frameCount + 1;
            ui.Close();
            ui.SetRefreshEnabled(!loading);
        }

        void OpenDetails(string id)
        {
            mode = Mode.Viewing;
            int request = ++detailRequest;
            ui.OpenDetails("正在读取问题详情…");
            StartCoroutine(api.Get(id, issue => {
                if (mode != Mode.Viewing || request != detailRequest) return;
                ui.ShowDetails(issue);
                if (savedMarkers.TryGetValue(id, out var marker)) marker.GetComponent<Renderer>().sharedMaterial = MaterialFor(issue.status);
            }, message => {
                if (mode == Mode.Viewing && request == detailRequest)
                    ui.OpenDetails("无法读取问题详情：\n" + message + "\n\n请关闭后刷新标记，或重新点击重试。");
            }));
        }

        void CloseDetails()
        {
            if (mode != Mode.Viewing) return;
            ++detailRequest; // Ignore responses that arrive after closing/reopening a different marker.
            ui.CloseDetails();
            mode = Mode.Browsing;
            ignoreClicksThroughFrame = Time.frameCount + 1;
            ui.SetRefreshEnabled(!loading);
        }

        void Refresh()
        {
            if (loading || mode != Mode.Browsing) return;
            loading = true;
            ignoreClicksThroughFrame = Time.frameCount + 1;
            ui.SetRefreshEnabled(false);
            ui.Notice("正在读取已保存的问题…", false);
            StartCoroutine(Load());
        }

        IEnumerator Load()
        {
            yield return api.List(issues => {
                var activeIds = new HashSet<string>();
                foreach (Issue issue in issues)
                {
                    if (issue == null || string.IsNullOrEmpty(issue.id) || issue.position == null) continue;
                    activeIds.Add(issue.id);
                    if (savedMarkers.TryGetValue(issue.id, out var marker)) marker.GetComponent<Renderer>().sharedMaterial = MaterialFor(issue.status);
                    else
                    {
                        marker = Marker("Issue " + issue.id, issue.position.ToVector3(), MaterialFor(issue.status));
                        MakeSelectable(marker, issue.id);
                        savedMarkers[issue.id] = marker;
                    }
                }
                var removedIds = new List<string>();
                foreach (var pair in savedMarkers)
                    if (!activeIds.Contains(pair.Key)) { Destroy(pair.Value); removedIds.Add(pair.Key); }
                foreach (string id in removedIds) savedMarkers.Remove(id);
                ui.Notice("已同步 " + savedMarkers.Count + " 个问题标记。红：待处理 / 蓝：处理中 / 绿：已解决", false);
            }, message => ui.Notice("读取失败：" + message + "  可点击右上角刷新重试。", true));
            loading = false;
            ui.SetRefreshEnabled(mode == Mode.Browsing);
        }

        GameObject Marker(string name, Vector3 position, Material material)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.layer = 2; // Pending markers do not intercept scene clicks.
            sphere.transform.position = position;
            sphere.transform.localScale = Vector3.one * .16f;
            sphere.GetComponent<Collider>().enabled = false;
            sphere.GetComponent<Renderer>().sharedMaterial = material;
            return sphere;
        }
        static void MakeSelectable(GameObject marker, string id)
        {
            marker.layer = MarkerLayer;
            var collider = marker.GetComponent<Collider>();
            collider.isTrigger = true; // No physical obstruction to the player.
            collider.enabled = true;
            var selectable = marker.GetComponent<IssueMarker>() ?? marker.AddComponent<IssueMarker>();
            selectable.issueId = id;
        }
        Material MaterialFor(string status) => status == "resolved" ? resolvedMaterial : status == "in_progress" ? progressMaterial : openMaterial;
        static Material MarkerMaterial(Color color) { var material = new Material(Shader.Find("Standard")); material.color = color; return material; }
        void OnDestroy()
        {
            Destroy(draftMaterial); Destroy(openMaterial); Destroy(progressMaterial); Destroy(resolvedMaterial);
        }
    }
}
