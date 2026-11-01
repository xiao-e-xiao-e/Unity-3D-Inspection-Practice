using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Inspection
{
    // UI is assembled in code so the small demo has no external prefab/font dependency.
    public class InspectionUI
    {
        readonly Font font;
        readonly GameObject overlay;
        GameObject detailOverlay;
        Text detailText;
        ScrollRect detailScroll;
        readonly InputField title, description;
        readonly Dropdown priority;
        readonly Text coordinate, formMessage, notice, submitText;
        readonly Button submit, cancel, refresh;
        readonly Color ink = new Color(.14f, .23f, .25f);
        readonly Color green = new Color(.12f, .32f, .30f);
        public string Title => title.text;
        public string Description => description.text;
        public string Priority => new[] { "low", "medium", "high" }[priority.value];

        public InspectionUI(Action onSubmit, Action onCancel, Action onRefresh, Action onCloseDetails)
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 18);
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var canvasObject = new GameObject("Inspection UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            var canvas = canvasObject.GetComponent<RectTransform>();

            var header = Box(canvas, "Header", 20, 20, 900, 87, new Color(.07f,.16f,.19f,.93f));
            Label(header, "虚拟巡检现场  /  设备间 01", 18, 11, 860, 29, 22, Color.white);
            Label(header, "WASD 移动 · 右键转向 · 点击表面上报 / 标记查看详情 · Esc 关闭", 18, 49, 860, 23, 15, new Color(.77f,.85f,.79f));
            refresh = Button(canvas, "刷新已保存标记", 1040, 26, 220, 46, onRefresh);
            var noticeBox = Box(canvas, "Notice", 20, 630, 1240, 70, new Color(.97f,.98f,.94f,.96f));
            notice = Label(noticeBox, "", 16, 10, 1208, 50, 17, ink);

            overlay = new GameObject("Issue form overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = overlay.GetComponent<RectTransform>();
            overlayRect.SetParent(canvas, false); Stretch(overlayRect);
            overlay.GetComponent<Image>().color = new Color(0,0,0,.45f);
            var panel = Box(overlayRect, "Issue form", 0, 0, 510, 600, new Color(.97f,.98f,.95f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f,.5f);
            panel.anchoredPosition = Vector2.zero;
            Label(panel, "上报现场问题", 28, 23, 454, 38, 27, ink);
            Label(panel, "标记已定位，请填写问题信息。", 28, 65, 454, 24, 15, new Color(.42f,.5f,.45f));
            Label(panel, "标题 *", 28, 109, 454, 25, 16, ink);
            title = Input(panel, "问题标题", 28, 139, 454, 45, false);
            title.characterLimit = 200;
            Label(panel, "描述（可选）", 28, 198, 454, 25, 16, ink);
            description = Input(panel, "补充设备现象、巡检情况…", 28, 228, 454, 88, true);
            description.characterLimit = 2000;
            Label(panel, "优先级", 28, 329, 95, 28, 16, ink);
            priority = MakeDropdown(panel, 128, 326, 354, 38);
            coordinate = Label(panel, "", 28, 381, 454, 28, 15, ink);
            formMessage = Label(panel, "", 28, 420, 454, 99, 15, ink);
            cancel = Button(panel, "取消", 28, 535, 140, 43, onCancel);
            cancel.GetComponent<Image>().color = new Color(.72f,.76f,.71f);
            submit = Button(panel, "提交问题", 184, 535, 298, 43, onSubmit);
            submitText = submit.GetComponentInChildren<Text>();
            overlay.SetActive(false);
            BuildDetailPanel(canvas, onCloseDetails);
        }

        void BuildDetailPanel(Transform canvas, Action onClose)
        {
            detailOverlay = new GameObject("Issue details overlay", typeof(RectTransform), typeof(Image));
            var overlayRect = detailOverlay.GetComponent<RectTransform>();
            overlayRect.SetParent(canvas, false); Stretch(overlayRect);
            detailOverlay.GetComponent<Image>().color = new Color(0, 0, 0, .45f);
            var panel = Box(overlayRect, "Issue details", 0, 0, 590, 590, new Color(.97f, .98f, .95f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = Vector2.zero;
            Label(panel, "问题详情", 28, 22, 534, 38, 27, ink);
            Label(panel, "可滚动查看完整内容 · 关闭后继续巡检", 28, 67, 534, 25, 14, green);
            var viewport = Box(panel, "Details viewport", 28, 110, 534, 385, Color.white);
            viewport.gameObject.AddComponent<RectMask2D>();
            detailText = Label(viewport, "", 12, 8, 510, 365, 17, ink);
            detailText.alignment = TextAnchor.UpperLeft;
            detailText.supportRichText = false;
            detailText.raycastTarget = true;
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailText.verticalOverflow = VerticalWrapMode.Overflow;
            detailText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            detailScroll = viewport.gameObject.AddComponent<ScrollRect>();
            detailScroll.viewport = viewport; detailScroll.content = detailText.rectTransform;
            detailScroll.horizontal = false; detailScroll.movementType = ScrollRect.MovementType.Clamped; detailScroll.scrollSensitivity = 24f;
            Button(panel, "关闭详情", 28, 525, 534, 43, onClose);
            detailOverlay.SetActive(false);
        }

        public void OpenDetails(string message)
        {
            detailOverlay.SetActive(true); detailText.text = message;
            refresh.interactable = false;
            Canvas.ForceUpdateCanvases(); detailScroll.verticalNormalizedPosition = 1f;
            EventSystem.current.SetSelectedGameObject(null);
        }
        public void ShowDetails(Issue issue)
        {
            string status = issue.status == "resolved" ? "已解决" : issue.status == "in_progress" ? "处理中" : "待处理";
            string priorityLabel = issue.priority == "high" ? "高" : issue.priority == "low" ? "低" : "中";
            OpenDetails("标题\n" + issue.title + "\n\n优先级：" + priorityLabel + "（" + issue.priority + "）   状态：" + status +
                "\n\n描述\n" + (string.IsNullOrEmpty(issue.description) ? "暂无描述" : issue.description) +
                $"\n\n位置\nx: {issue.position.x}\ny: {issue.position.y}\nz: {issue.position.z}" +
                "\n\n创建时间：" + LocalTime(issue.createdAt) + "\n最后更新：" + LocalTime(issue.updatedAt) + "\n\n问题 ID\n" + issue.id + "\n");
        }
        static string LocalTime(string value) => DateTimeOffset.TryParse(value, out var time) ? time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") : value;
        public void CloseDetails() { detailOverlay.SetActive(false); EventSystem.current.SetSelectedGameObject(null); }

        public void Open(Vector3 position)
        {
            overlay.SetActive(true);
            title.text = description.text = "";
            priority.value = 1;
            coordinate.text = $"命中位置  x: {position.x:F3}   y: {position.y:F3}   z: {position.z:F3}";
            formMessage.text = "";
            SetSubmitting(false, false);
            refresh.interactable = false;
            EventSystem.current.SetSelectedGameObject(title.gameObject);
            title.ActivateInputField();
        }
        public void Close() { overlay.SetActive(false); EventSystem.current.SetSelectedGameObject(null); }
        public void SetSubmitting(bool submitting, bool frozen)
        {
            submit.interactable = cancel.interactable = !submitting;
            title.interactable = description.interactable = priority.interactable = !frozen;
            submitText.text = submitting ? "提交中…" : frozen ? "重试提交" : "提交问题";
        }
        public void SetRefreshEnabled(bool enabled) { refresh.interactable = enabled; }
        public void FormMessage(string message, bool error) { formMessage.text = message; formMessage.color = error ? new Color(.7f,.23f,.17f) : green; }
        public void Notice(string message, bool error) { notice.text = message; notice.color = error ? new Color(.7f,.23f,.17f) : ink; }

        RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(width,height);
            return rect;
        }
        RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var rect = Rect(parent,name,x,y,width,height); rect.gameObject.AddComponent<Image>().color = color; return rect;
        }
        Text Label(Transform parent, string text, float x, float y, float width, float height, int size, Color color)
        {
            var label = Rect(parent,text.Length>0 ? text : "Text",x,y,width,height).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            return label;
        }
        Button Button(Transform parent, string caption, float x, float y, float width, float height, Action action)
        {
            var rect = Box(parent,caption,x,y,width,height,green);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.disabledColor = new Color(.65f,.65f,.65f,.7f); button.colors = colors;
            var label = Label(rect,caption,0,0,width,height,17,Color.white); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(()=>action()); return button;
        }
        InputField Input(Transform parent, string hint, float x, float y, float width, float height, bool multiline)
        {
            var rect = Box(parent,hint,x,y,width,height,Color.white);
            var input = rect.gameObject.AddComponent<InputField>();
            var text = Label(rect,"",12,6,width-24,height-12,17,ink);
            var placeholder = Label(rect,hint,12,6,width-24,height-12,16,new Color(.6f,.65f,.6f));
            text.alignment = placeholder.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            input.textComponent = text; input.placeholder = placeholder; input.targetGraphic = rect.GetComponent<Image>();
            input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            return input;
        }
        Dropdown MakeDropdown(Transform parent, float x, float y, float width, float height)
        {
            var rect = Box(parent,"Priority",x,y,width,height,Color.white);
            var dropdown = rect.gameObject.AddComponent<Dropdown>(); dropdown.targetGraphic = rect.GetComponent<Image>();
            dropdown.captionText = Label(rect,"",12,0,width-40,height,16,ink);
            Label(rect,"▾",width-28,0,24,height,17,ink);
            var template = Box(rect,"Template",0,0,width,114,Color.white);
            template.anchorMin = new Vector2(0,0); template.anchorMax = new Vector2(1,0);
            template.pivot = new Vector2(.5f,1); template.sizeDelta = new Vector2(0,114); template.anchoredPosition = Vector2.zero;
            var viewport = Box(template,"Viewport",0,0,width,114,Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect(viewport,"Content",0,0,width,38);
            var item = Box(content,"Item",0,0,width,38,new Color(.91f,.94f,.87f));
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            var check = Label(item,"✓",8,0,25,38,17,green); toggle.graphic = check;
            dropdown.itemText = Label(item,"",38,0,width-48,38,16,ink);
            var scroll = template.gameObject.AddComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            dropdown.template = template;
            dropdown.options = new List<Dropdown.OptionData> { new Dropdown.OptionData("低 · low"), new Dropdown.OptionData("中 · medium"), new Dropdown.OptionData("高 · high") };
            dropdown.value = 1; dropdown.RefreshShownValue(); template.gameObject.SetActive(false);
            return dropdown;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
