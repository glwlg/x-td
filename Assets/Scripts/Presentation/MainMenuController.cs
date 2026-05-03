using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using XTD.Content;
using XTD.Flow;
using XTD.Roguelike;

namespace XTD.Presentation
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private ContentCatalog catalog;
        private static Font cachedFont;
        private static readonly Dictionary<string, Sprite> cachedResourceSprites = new();
        private static Sprite cachedCircleSprite;
        private GameFlowController flow;
        private GameObject uiRoot;
        private SidePanelMode sidePanelMode = SidePanelMode.None;

        private enum SidePanelMode
        {
            None,
            Deck,
            Artifacts,
            Progress,
            Codex,
            Log
        }

        private void Start()
        {
            catalog ??= DemoContentFactory.CreateCatalog();
            DemoContentFactory.EnsureCatalogComplete(catalog);
            flow = GameFlowController.EnsureInstance();
            flow.ConfigureCatalog(catalog);
            BuildUi();
        }

        private void BuildUi()
        {
            if (uiRoot != null)
            {
                Destroy(uiRoot);
            }

            uiRoot = new GameObject("主菜单界面");
            var canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            uiRoot.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            BuildBackdrop(uiRoot.transform);

            if (flow.CurrentRun != null && flow.CurrentRun.isComplete)
            {
                BuildRunEnd("探索完成", "最终首领已被击败，本次迷宫探索结束。");
                return;
            }

            if (flow.CurrentRun != null && flow.CurrentRun.isDefeated)
            {
                BuildRunEnd("探索失败", "主角生命归零，本次迷宫探索结束。");
                return;
            }

            if (!flow.HasActiveRun)
            {
                BuildTitleMenu();
                return;
            }

            if (flow.HasPendingCardReward)
            {
                BuildCardRewardPanel();
                return;
            }

            if (flow.HasPendingNode)
            {
                BuildNodePanel(flow.PendingNode);
                return;
            }

            BuildMapPanel();
        }

        private void BuildTitleMenu()
        {
            var title = CreateText("标题", uiRoot.transform, new Vector2(0.5f, 0.78f), new Vector2(860f, 110f), 56, TextAnchor.MiddleCenter);
            title.text = "神魔镇荒";

            var subtitle = CreateText("副标题", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(920f, 68f), 24, TextAnchor.MiddleCenter);
            subtitle.text = "肉鸽爬塔 · 正面对抗 · 卡牌阵地";
            subtitle.color = new Color(1f, 0.86f, 0.48f, 0.95f);

            var progress = flow.PermanentProgress;
            var progressText = CreateText("永久进度", uiRoot.transform, new Vector2(0.5f, 0.625f), new Vector2(920f, 42f), 20, TextAnchor.MiddleCenter);
            progressText.text = $"永久进度：探索 {progress.totalRuns} 次    通关 {progress.completedRuns} 次    总经验 {progress.totalHeroExperience}    主角等级 {flow.PermanentHeroLevel()}";
            progressText.color = new Color(0.94f, 0.90f, 0.80f, 0.90f);

            var classTitle = CreateText("职业选择标题", uiRoot.transform, new Vector2(0.5f, 0.565f), new Vector2(820f, 42f), 24, TextAnchor.MiddleCenter);
            classTitle.text = "选择本次探索职业";
            classTitle.color = new Color(1f, 0.88f, 0.52f, 0.96f);

            var classes = new[]
            {
                HeroClassType.BorderCommander,
                HeroClassType.SpiritSummoner,
                HeroClassType.ThunderMage
            };
            for (var i = 0; i < classes.Length; i++)
            {
                CreateHeroClassChoice(classes[i], new Vector2(-360f + i * 360f, 0f));
            }

            var battle = CreateButton("只看战斗原型", uiRoot.transform, new Vector2(0.5f, 0.185f), new Vector2(320f, 60f));
            battle.onClick.AddListener(() => SceneManager.LoadScene("BattlePrototype"));

            var quit = CreateButton("退出", uiRoot.transform, new Vector2(0.5f, 0.105f), new Vector2(260f, 54f));
            quit.onClick.AddListener(Application.Quit);
        }

        private void CreateHeroClassChoice(HeroClassType heroClass, Vector2 offset)
        {
            var button = CreateButton(string.Empty, uiRoot.transform, new Vector2(0.5f, 0.385f), new Vector2(336f, 320f), offset);
            button.GetComponent<Image>().color = heroClass switch
            {
                HeroClassType.SpiritSummoner => new Color(0.10f, 0.24f, 0.18f, 0.92f),
                HeroClassType.ThunderMage => new Color(0.18f, 0.17f, 0.34f, 0.92f),
                _ => new Color(0.21f, 0.16f, 0.09f, 0.92f)
            };
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.92f, 0.74f, 0.36f, 0.56f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var classArt = AddSpriteIcon(button.transform, LoadHeroClassSprite(heroClass), new Vector2(0f, 38f), new Vector2(292f, 140f));
            if (classArt != null)
            {
                classArt.color = new Color(1f, 1f, 1f, 0.58f);
            }

            var name = CreateText("职业名称", button.transform, new Vector2(0.5f, 0.86f), new Vector2(286f, 48f), 30, TextAnchor.MiddleCenter);
            name.text = GameFlowController.HeroClassName(heroClass);
            name.color = new Color(1f, 0.92f, 0.72f, 0.98f);
            name.raycastTarget = false;
            AddTextShadow(name, new Color(0.02f, 0.012f, 0f, 0.92f), new Vector2(1.5f, -1.5f));

            var style = CreateText("职业流派", button.transform, new Vector2(0.5f, 0.52f), new Vector2(286f, 38f), 20, TextAnchor.MiddleCenter);
            style.text = GameFlowController.HeroClassShortStyle(heroClass);
            style.color = new Color(0.82f, 0.96f, 0.90f, 0.94f);
            style.raycastTarget = false;
            AddTextShadow(style, new Color(0.01f, 0.03f, 0.02f, 0.88f), new Vector2(1.2f, -1.2f));

            var desc = CreateText("职业描述", button.transform, new Vector2(0.5f, 0.30f), new Vector2(286f, 104f), 18, TextAnchor.MiddleCenter);
            desc.text = GameFlowController.HeroClassDescription(heroClass);
            desc.color = new Color(0.94f, 0.92f, 0.84f, 0.95f);
            desc.raycastTarget = false;

            var action = CreateText("职业按钮提示", button.transform, new Vector2(0.5f, 0.10f), new Vector2(210f, 34f), 20, TextAnchor.MiddleCenter);
            action.text = "开始探索";
            action.color = new Color(1f, 0.86f, 0.48f, 0.98f);
            action.raycastTarget = false;

            button.onClick.AddListener(() => flow.StartNewRun(catalog, heroClass));
        }

        private void BuildRunEnd(string titleText, string body)
        {
            BuildHeader();
            var title = CreateText("结算标题", uiRoot.transform, new Vector2(0.5f, 0.64f), new Vector2(760f, 96f), 48, TextAnchor.MiddleCenter);
            title.text = titleText;

            var summary = CreateText("结算内容", uiRoot.transform, new Vector2(0.5f, 0.52f), new Vector2(1080f, 180f), 24, TextAnchor.MiddleCenter);
            var run = flow.CurrentRun;
            var permanent = flow.PermanentProgress;
            var permanentNames = permanent.permanentArtifactIds.Count == 0
                ? "暂无"
                : string.Join("、", permanent.permanentArtifactIds
                    .Select(id => catalog.FindArtifact(id)?.displayName ?? id)
                    .Take(4));
            summary.text =
                $"{body}\n" +
                $"本局：{GameFlowController.HeroClassName(run.heroClass)}    金币 {run.gold}    经验 +{run.heroExperience}    神器 {run.artifactIds.Count}    卡组 {run.deckCardIds.Count}\n" +
                $"永久：探索 {permanent.totalRuns} 次    通关 {permanent.completedRuns} 次    总经验 {permanent.totalHeroExperience}    主角等级 {flow.PermanentHeroLevel()}\n" +
                $"永久神器：{permanentNames}";

            var restart = CreateButton("重新开始探索", uiRoot.transform, new Vector2(0.5f, 0.36f), new Vector2(320f, 76f));
            restart.onClick.AddListener(() => flow.StartNewRun(catalog));

            var menu = CreateButton("返回标题", uiRoot.transform, new Vector2(0.5f, 0.26f), new Vector2(280f, 62f));
            menu.onClick.AddListener(() => flow.ReturnToTitle());
        }

        private void BuildMapPanel()
        {
            BuildHeader();
            BuildSideMenu();
            BuildDebugStrip();
            BuildSideDetailPanel();

            var run = flow.CurrentRun;
            var subtitle = CreateText("当前消息", uiRoot.transform, new Vector2(0.5f, 0.875f), new Vector2(1040f, 34f), 20, TextAnchor.MiddleCenter);
            subtitle.text = string.IsNullOrWhiteSpace(run.lastMessage) ? "边境指挥官正在选择下一处房间。" : run.lastMessage;
            subtitle.color = new Color(0.94f, 0.90f, 0.80f, 0.92f);

            var title = CreateText("迷宫标题", uiRoot.transform, new Vector2(0.5f, 0.825f), new Vector2(920f, 68f), 42, TextAnchor.MiddleCenter);
            title.text = $"迷宫 {run.floor} · {FloorSceneName(run.floor)}";
            title.color = new Color(1f, 0.88f, 0.52f, 0.98f);

            var affixText = CreateText("层词缀", uiRoot.transform, new Vector2(0.5f, 0.765f), new Vector2(1040f, 42f), 19, TextAnchor.MiddleCenter);
            affixText.text = $"{flow.CurrentFloorAffixName()}：{flow.CurrentFloorAffixDescription()}";
            affixText.color = new Color(1f, 0.86f, 0.48f, 0.95f);

            var currentChoices = flow.CurrentChoices().Select(node => node.Key).ToHashSet();
            var selectedNodes = flow.CurrentRun.selectedNodeKeys.ToHashSet();
            var floorRows = flow.MapRows
                .Where(row => row.Count > 0 && row[0].Floor == flow.CurrentRun.floor)
                .OrderBy(row => row[0].Row)
                .ToList();

            var mapPanel = CreatePanel("层路线图", uiRoot.transform, new Vector2(0.5f, 0.47f), new Vector2(0.5f, 0.47f), Vector2.zero, new Vector2(1260f, 640f), new Color(0.02f, 0.03f, 0.035f, 0.18f));
            mapPanel.raycastTarget = false;
            var mapRoot = mapPanel.rectTransform;
            var previewPanel = CreatePanel("房间情报底", uiRoot.transform, new Vector2(0.84f, 0.70f), new Vector2(0.84f, 0.70f), Vector2.zero, new Vector2(370f, 132f), new Color(0.025f, 0.030f, 0.035f, 0.56f));
            previewPanel.gameObject.AddComponent<Outline>().effectColor = new Color(0.70f, 0.58f, 0.35f, 0.42f);
            var previewText = CreateText("房间预览", previewPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(330f, 110f), 18, TextAnchor.MiddleLeft);
            previewText.text = "把鼠标移到房间上，可以预览类型、奖励和风险。";
            previewText.color = new Color(0.92f, 0.94f, 0.88f, 0.94f);
            var positions = new Dictionary<string, Vector2>();
            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    positions[node.Key] = NodeMapPosition(row, node);
                }
            }

            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    if (!positions.TryGetValue(node.Key, out var from))
                    {
                        continue;
                    }

                    var nextRow = floorRows.FirstOrDefault(candidate => candidate.Count > 0 && candidate[0].Row == node.Row + 1);
                    if (nextRow == null)
                    {
                        continue;
                    }

                    foreach (var nextIndex in node.NextNodeIndices)
                    {
                        var nextNode = nextRow.FirstOrDefault(candidate => candidate.NodeIndex == nextIndex);
                        if (nextNode == null || !positions.TryGetValue(nextNode.Key, out var to))
                        {
                            continue;
                        }

                        var selectedLine = selectedNodes.Contains(node.Key) && selectedNodes.Contains(nextNode.Key);
                        var activeLine = selectedNodes.Contains(node.Key) || currentChoices.Contains(node.Key);
                        var color = selectedLine
                            ? new Color(1f, 0.76f, 0.24f, 0.92f)
                            : activeLine
                                ? new Color(0.68f, 0.84f, 0.78f, 0.64f)
                                : new Color(0.50f, 0.56f, 0.58f, 0.30f);
                        CreateMapLine(mapRoot, from, to, color, selectedLine ? 5.5f : 3f);
                    }
                }
            }

            foreach (var row in floorRows)
            {
                foreach (var node in row)
                {
                    var isAvailable = currentChoices.Contains(node.Key);
                    var isSelected = selectedNodes.Contains(node.Key);
                    var position = positions[node.Key];
                    var button = CreateMapRoomButton(node, mapRoot, position, isAvailable, isSelected);
                    button.interactable = isAvailable;
                    AddNodePreview(button, node, previewText, isAvailable || isSelected);
                    if (!isAvailable)
                    {
                        continue;
                    }

                    var selectedNode = node;
                    button.onClick.AddListener(() =>
                    {
                        flow.SelectNode(selectedNode);
                        if (!IsBattleNode(selectedNode.NodeType))
                        {
                            BuildUi();
                        }
                    });
                }
            }

            var progress = CreateText("路线说明", uiRoot.transform, new Vector2(0.5f, 0.095f), new Vector2(1100f, 56f), 20, TextAnchor.MiddleCenter);
            progress.text = "从下往上选择房间。亮起的房间可以进入，金色房间是已走路径；连线决定下一步可选路线。";
            progress.color = new Color(0.90f, 0.92f, 0.88f, 0.92f);

            var back = CreateButton("返回标题", uiRoot.transform, new Vector2(0.93f, 0.10f), new Vector2(112f, 62f));
            back.GetComponent<Image>().color = new Color(0.14f, 0.12f, 0.10f, 0.86f);
            back.onClick.AddListener(() => flow.ReturnToTitle());
        }

        private static Vector2 NodeMapPosition(IReadOnlyList<MapNodeRuntime> row, MapNodeRuntime node)
        {
            var center = (row.Count - 1) * 0.5f;
            var x = (node.NodeIndex - center) * 230f;
            var y = -270f + (node.Row - 1) * 62f;
            return new Vector2(x, y);
        }

        private Button CreateMapRoomButton(MapNodeRuntime node, Transform parent, Vector2 offset, bool available, bool selected)
        {
            var button = CreateButton(string.Empty, parent, new Vector2(0.5f, 0.5f), new Vector2(86f, 86f), offset);
            var image = button.GetComponent<Image>();
            image.sprite = CircleSprite();
            image.color = selected
                ? new Color(0.92f, 0.64f, 0.18f, 0.95f)
                : available
                    ? WithAlpha(NodeColor(node.NodeType), 0.92f)
                    : new Color(0.10f, 0.12f, 0.13f, 0.70f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var halo = AddNodeHalo(button.transform, selected, available);
            halo.transform.SetAsFirstSibling();
            AddSpriteIcon(button.transform, LoadNodeSprite(node.NodeType), new Vector2(0f, 6f), new Vector2(54f, 54f));

            var labelPanel = CreatePanel("节点标签底", button.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -38f), new Vector2(96f, 24f), new Color(0.02f, 0.02f, 0.018f, 0.72f));
            labelPanel.raycastTarget = false;
            var nodeLabel = CreateText("节点标签", labelPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(90f, 20f), 16, TextAnchor.MiddleCenter);
            nodeLabel.text = NodeShortName(node.NodeType);
            nodeLabel.color = selected ? new Color(1f, 0.88f, 0.42f) : available ? Color.white : new Color(0.70f, 0.73f, 0.72f, 0.86f);
            nodeLabel.raycastTarget = false;

            if (selected || available)
            {
                var outline = button.gameObject.AddComponent<Outline>();
                outline.effectColor = selected ? new Color(1f, 0.82f, 0.32f, 0.98f) : new Color(0.78f, 0.92f, 1f, 0.78f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            return button;
        }

        private void AddNodePreview(Button button, MapNodeRuntime node, Text previewText, bool reachable)
        {
            var trigger = button.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerEnter, () => previewText.text = BuildNodePreview(node, reachable));
            AddTrigger(trigger, EventTriggerType.PointerClick, () => previewText.text = BuildNodePreview(node, reachable));
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
        }

        private static string BuildNodePreview(MapNodeRuntime node, bool reachable)
        {
            var state = reachable ? "当前路线可达" : "暂未连通";
            return node.NodeType switch
            {
                MapNodeType.NormalMonster => $"普通怪物\n{state}\n奖励：金币\n目标：摧毁敌方基地。",
                MapNodeType.EliteMonster => $"精英怪物\n{state}\n奖励：金币，从 3 张卡中选 1 张\n提示：敌方核心站桩输出并派兵。",
                MapNodeType.Shop => $"商店\n{state}\n奖励：构筑调整\n提示：购买新牌，或半价出售已有牌。",
                MapNodeType.Rest => $"休息\n{state}\n奖励：回血或三合一合成\n提示：合成最多升到 3 级。",
                MapNodeType.Opportunity => $"机遇\n{state}\n奖励：事件收益\n提示：可能获得金币、卡牌、升级或承担小风险。",
                MapNodeType.Mystery => $"神秘\n{state}\n奖励/风险：高收益或凶阵战斗\n提示：凶阵打赢也没有额外奖励。",
                MapNodeType.Artifact => $"神器层\n{state}\n奖励：3 个神器选 1 个\n提示：观星镜可提高可选数量。",
                MapNodeType.SmallBoss => $"小首领\n{state}\n奖励：金币、经验，从 4 张卡中选 2 张\n提示：核心生命低时技能节奏会加快。",
                MapNodeType.FinalBoss => $"最终首领\n{state}\n奖励：大量金币、经验、永久神器，从 5 张卡中选 3 张\n提示：击败后结束本次探索。",
                _ => $"房间\n{state}\n奖励：未知"
            };
        }

        private static Image CreateMapLine(Transform parent, Vector2 from, Vector2 to, Color color, float thickness)
        {
            var go = new GameObject("路线", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static string NodeShortName(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => "普通",
                MapNodeType.EliteMonster => "精英",
                MapNodeType.Shop => "商店",
                MapNodeType.Rest => "休息",
                MapNodeType.Opportunity => "机遇",
                MapNodeType.Mystery => "神秘",
                MapNodeType.Artifact => "神器",
                MapNodeType.SmallBoss => "小首领",
                MapNodeType.FinalBoss => "最终首领",
                _ => "房间"
            };
        }

        private static string NodeIconName(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => "node_normal_monster",
                MapNodeType.EliteMonster => "node_elite_monster",
                MapNodeType.Shop => "node_shop",
                MapNodeType.Rest => "node_rest",
                MapNodeType.Opportunity => "node_opportunity",
                MapNodeType.Mystery => "node_mystery",
                MapNodeType.Artifact => "node_artifact",
                MapNodeType.SmallBoss => "node_small_boss",
                MapNodeType.FinalBoss => "node_final_boss",
                _ => "icon_route_path"
            };
        }

        private static Color NodeColor(MapNodeType nodeType)
        {
            return nodeType switch
            {
                MapNodeType.NormalMonster => new Color(0.19f, 0.24f, 0.22f, 0.96f),
                MapNodeType.EliteMonster => new Color(0.36f, 0.17f, 0.13f, 0.96f),
                MapNodeType.Shop => new Color(0.28f, 0.21f, 0.08f, 0.96f),
                MapNodeType.Rest => new Color(0.12f, 0.28f, 0.22f, 0.96f),
                MapNodeType.Opportunity => new Color(0.22f, 0.20f, 0.36f, 0.96f),
                MapNodeType.Mystery => new Color(0.16f, 0.13f, 0.22f, 0.96f),
                MapNodeType.Artifact => new Color(0.31f, 0.20f, 0.08f, 0.96f),
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => new Color(0.42f, 0.08f, 0.08f, 0.96f),
                _ => new Color(0.15f, 0.2f, 0.28f, 0.95f)
            };
        }

        private static string FloorSceneName(int floor)
        {
            return floor switch
            {
                1 => "迷雾谷",
                2 => "雷劫台",
                3 => "混沌宫",
                _ => "未知境"
            };
        }

        private static string ArtifactRarityName(ArtifactRarity rarity)
        {
            return rarity switch
            {
                ArtifactRarity.Common => "阵法",
                ArtifactRarity.Rare => "奇术",
                ArtifactRarity.Epic => "法宝",
                ArtifactRarity.Legendary => "神物",
                _ => "强化"
            };
        }

        private static Color ArtifactRarityColor(ArtifactRarity rarity)
        {
            return rarity switch
            {
                ArtifactRarity.Common => new Color(0.48f, 0.86f, 0.72f, 0.92f),
                ArtifactRarity.Rare => new Color(0.70f, 0.56f, 1f, 0.92f),
                ArtifactRarity.Epic => new Color(1f, 0.72f, 0.28f, 0.94f),
                ArtifactRarity.Legendary => new Color(1f, 0.35f, 0.28f, 0.96f),
                _ => new Color(0.86f, 0.82f, 0.72f, 0.92f)
            };
        }

        private static Color ArtifactPanelColor(ArtifactRarity rarity)
        {
            var color = rarity switch
            {
                ArtifactRarity.Common => new Color(0.03f, 0.12f, 0.10f, 0.93f),
                ArtifactRarity.Rare => new Color(0.10f, 0.06f, 0.17f, 0.94f),
                ArtifactRarity.Epic => new Color(0.18f, 0.11f, 0.035f, 0.94f),
                ArtifactRarity.Legendary => new Color(0.20f, 0.055f, 0.04f, 0.94f),
                _ => new Color(0.08f, 0.08f, 0.09f, 0.94f)
            };
            return color;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private static Image AddNodeHalo(Transform parent, bool selected, bool available)
        {
            var go = new GameObject("徽章光环", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(104f, 104f);
            rect.anchoredPosition = Vector2.zero;
            var halo = go.GetComponent<Image>();
            halo.sprite = CircleSprite();
            halo.raycastTarget = false;
            halo.color = selected
                ? new Color(1f, 0.74f, 0.22f, 0.36f)
                : available
                    ? new Color(0.72f, 0.92f, 1f, 0.20f)
                    : new Color(0.04f, 0.04f, 0.04f, 0.12f);
            return halo;
        }

        private static Sprite CircleSprite()
        {
            if (cachedCircleSprite != null)
            {
                return cachedCircleSprite;
            }

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "XTD_RuntimeCircle"
            };

            var pixels = new Color[size * size];
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / center;
                    var dy = (y - center) / center;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01((1f - distance) * 12f);
                    var rim = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.80f) * 16f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(alpha * 0.82f, rim * 0.55f));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            cachedCircleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return cachedCircleSprite;
        }

        private static string CardTypeName(CardType type)
        {
            if (type == CardType.Curse)
            {
                return "诅咒";
            }

            return type switch
            {
                CardType.Structure => "建筑",
                CardType.Soldier => "士兵",
                CardType.EliteSoldier => "精兵",
                CardType.Hero => "英雄",
                CardType.Spell => "法术",
                CardType.Tactic => "战术",
                CardType.Debuff => "压制",
                CardType.Economy => "调度",
                _ => "卡牌"
            };
        }

        private static Color CardPanelColor(CardType type)
        {
            if (type == CardType.Curse)
            {
                return new Color(0.14f, 0.06f, 0.18f, 0.96f);
            }

            return type switch
            {
                CardType.Structure => new Color(0.33f, 0.18f, 0.10f, 0.96f),
                CardType.Spell => new Color(0.34f, 0.09f, 0.08f, 0.96f),
                CardType.Tactic => new Color(0.14f, 0.22f, 0.14f, 0.96f),
                CardType.Debuff => new Color(0.20f, 0.10f, 0.24f, 0.96f),
                CardType.Economy => new Color(0.16f, 0.18f, 0.28f, 0.96f),
                CardType.EliteSoldier or CardType.Hero => new Color(0.30f, 0.22f, 0.08f, 0.96f),
                _ => new Color(0.13f, 0.20f, 0.23f, 0.96f)
            };
        }

        private void BuildLegacyChoicePanel()
        {
            var choices = flow.CurrentChoices();
            var startX = -((choices.Count - 1) * 220f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var node = choices[i];
                var button = CreateNodeButton(node, uiRoot.transform, new Vector2(0.5f, 0.55f), new Vector2(startX + i * 220f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.SelectNode(node);
                    if (!IsBattleNode(node.NodeType))
                    {
                        BuildUi();
                    }
                });
            }
        }

        private void BuildNodePanel(MapNodeRuntime node)
        {
            BuildHeader();
            BuildDebugStrip();

            if (node.NodeType != MapNodeType.Artifact)
            {
                var title = CreateText("节点标题", uiRoot.transform, new Vector2(0.5f, 0.77f), new Vector2(900f, 72f), 36, TextAnchor.MiddleCenter);
                title.text = $"{GameFlowController.NodeTypeName(node.NodeType)}";
            }

            switch (node.NodeType)
            {
                case MapNodeType.Shop:
                    BuildShopPanel();
                    break;
                case MapNodeType.Rest:
                    BuildRestPanel();
                    break;
                case MapNodeType.Opportunity:
                    BuildOpportunityChoicesPanel();
                    break;
                case MapNodeType.Mystery:
                    BuildMysteryPanel();
                    break;
                case MapNodeType.Artifact:
                    BuildArtifactPanel();
                    break;
                default:
                    BuildMapPanel();
                    break;
            }
        }

        private void BuildCardRewardPanel()
        {
            BuildHeader();

            var title = CreateText("奖励标题", uiRoot.transform, new Vector2(0.5f, 0.74f), new Vector2(920f, 72f), 36, TextAnchor.MiddleCenter);
            title.text = $"卡牌奖励：还可选择 {flow.PendingCardRewardPickCount} 张";

            var choices = flow.PendingCardRewardChoices();
            var startX = -((choices.Count - 1) * 230f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var card = choices[i];
                var label = $"{card.displayName}\n{CardTypeName(card.type)}  费用 {card.cost}  Lv.{card.level}\n{card.description}";
                var button = CreateButton(label, uiRoot.transform, new Vector2(0.5f, 0.50f), new Vector2(220f, 190f), new Vector2(startX + i * 230f, 0f));
                button.GetComponent<Image>().color = CardPanelColor(card.type);
                button.onClick.AddListener(() =>
                {
                    flow.ChooseCardReward(card);
                    BuildUi();
                });
            }

            var skip = CreateButton("放弃剩余奖励", uiRoot.transform, new Vector2(0.5f, 0.24f), new Vector2(260f, 62f));
            skip.onClick.AddListener(() =>
            {
                flow.SkipCardRewardForGold();
                BuildUi();
            });
        }

        private void BuildShopPanel()
        {
            var info = CreateText("商店说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1000f, 52f), 22, TextAnchor.MiddleCenter);
            info.text = $"当前金币 {flow.CurrentRun.gold}。购买会移出当前货架；可以刷新商品、半价出售，或花金币净化移除 1 张牌。";

            var shopCards = flow.GenerateShopCards();
            if (shopCards.Count == 0)
            {
                var empty = CreateText("商店售罄", uiRoot.transform, new Vector2(0.5f, 0.56f), new Vector2(720f, 70f), 24, TextAnchor.MiddleCenter);
                empty.text = "当前货架已经买空，可以刷新商品。";
                empty.color = new Color(0.94f, 0.90f, 0.78f, 0.95f);
            }

            for (var i = 0; i < shopCards.Count; i++)
            {
                var card = shopCards[i];
                var button = CreateButton($"{card.displayName}\n{CardTypeName(card.type)} Lv.{card.level}\n买 {flow.CardBuyPrice(card)} 金币", uiRoot.transform, new Vector2(0.5f, 0.56f), new Vector2(190f, 118f), new Vector2(-390f + i * 195f, 0f));
                button.GetComponent<Image>().color = CardPanelColor(card.type);
                button.onClick.AddListener(() =>
                {
                    flow.BuyCard(card);
                    BuildUi();
                });
            }

            var reroll = CreateButton($"刷新货架\n{flow.ShopRerollCost} 金币", uiRoot.transform, new Vector2(0.83f, 0.56f), new Vector2(210f, 78f));
            reroll.GetComponent<Image>().color = flow.CurrentRun.gold >= flow.ShopRerollCost
                ? new Color(0.20f, 0.16f, 0.10f, 0.94f)
                : new Color(0.08f, 0.08f, 0.08f, 0.70f);
            reroll.onClick.AddListener(() =>
            {
                flow.RerollShopCards();
                BuildUi();
            });

            var sellTitle = CreateText("出售标题", uiRoot.transform, new Vector2(0.5f, 0.425f), new Vector2(1000f, 42f), 20, TextAnchor.MiddleCenter);
            sellTitle.text = "半价出售已有卡牌";
            var sellCards = flow.CurrentRun.deckCardIds
                .GroupBy(id => id)
                .Select(group => new { Id = group.Key, Count = group.Count(), Card = catalog.FindCard(group.Key) })
                .Where(entry => entry.Card != null)
                .OrderBy(entry => entry.Card.type)
                .ThenBy(entry => entry.Card.cost)
                .ThenBy(entry => entry.Card.displayName)
                .Take(7)
                .ToList();
            for (var i = 0; i < sellCards.Count; i++)
            {
                var entry = sellCards[i];
                var button = CreateButton($"{entry.Card.displayName} x{entry.Count}\n卖 {Mathf.Max(1, flow.CardBuyPrice(entry.Card) / 2)}", uiRoot.transform, new Vector2(0.5f, 0.34f), new Vector2(178f, 78f), new Vector2(-535f + i * 178f, 0f));
                button.GetComponent<Image>().color = WithAlpha(CardPanelColor(entry.Card.type), 0.86f);
                button.onClick.AddListener(() =>
                {
                    flow.SellCard(entry.Id);
                    BuildUi();
                });
            }

            var removeTitle = CreateText("净化标题", uiRoot.transform, new Vector2(0.5f, 0.255f), new Vector2(1000f, 38f), 19, TextAnchor.MiddleCenter);
            removeTitle.text = flow.CanRemoveCardAtShop
                ? $"净化移除一张牌：{flow.ShopRemoveCost} 金币，本次商店限 1 次"
                : "净化移除：本次不可用，可能已使用、金币不足或卡组过薄";
            removeTitle.color = new Color(0.95f, 0.90f, 0.78f, 0.94f);

            var removeCards = sellCards.Take(5).ToList();
            for (var i = 0; i < removeCards.Count; i++)
            {
                var entry = removeCards[i];
                var button = CreateButton($"{entry.Card.displayName}\n净化", uiRoot.transform, new Vector2(0.5f, 0.185f), new Vector2(178f, 64f), new Vector2(-356f + i * 178f, 0f));
                button.GetComponent<Image>().color = flow.CanRemoveCardAtShop && flow.CurrentRun.gold >= flow.ShopRemoveCost
                    ? new Color(0.16f, 0.10f, 0.19f, 0.92f)
                    : new Color(0.08f, 0.08f, 0.08f, 0.66f);
                button.interactable = flow.CanRemoveCardAtShop;
                button.onClick.AddListener(() =>
                {
                    flow.RemoveCardAtShop(entry.Id);
                    BuildUi();
                });
            }

            var leave = CreateButton("离开商店", uiRoot.transform, new Vector2(0.5f, 0.19f), new Vector2(260f, 66f));
            leave.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -92f);
            leave.onClick.AddListener(() =>
            {
                flow.LeaveShop();
                BuildUi();
            });
        }

        private void BuildRestPanel()
        {
            var info = CreateText("休息说明", uiRoot.transform, new Vector2(0.5f, 0.69f), new Vector2(1000f, 52f), 22, TextAnchor.MiddleCenter);
            info.text = "选择回血，或三张同级同名卡牌合成一张高一级卡牌。每次休息只能做一件事。";

            var heal = CreateButton("随机回血 10%-30%", uiRoot.transform, new Vector2(0.5f, 0.57f), new Vector2(300f, 74f));
            heal.onClick.AddListener(() =>
            {
                flow.TakeRestHeal();
                BuildUi();
            });

            var groups = flow.UpgradableCardGroups().ToList();
            var label = CreateText("合成标题", uiRoot.transform, new Vector2(0.5f, 0.46f), new Vector2(1000f, 42f), 20, TextAnchor.MiddleCenter);
            label.text = groups.Count > 0 ? "可合成卡牌" : "当前没有三张同级同名卡牌";

            for (var i = 0; i < groups.Count && i < 5; i++)
            {
                var group = groups[i];
                var card = catalog.FindCard(group.Key);
                var button = CreateButton($"{card.displayName}\n三合一", uiRoot.transform, new Vector2(0.5f, 0.35f), new Vector2(190f, 80f), new Vector2(-390f + i * 195f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.UpgradeCardsAtRest(group.Key);
                    BuildUi();
                });
            }
        }

        private void BuildOpportunityPanel()
        {
            var body = CreateText("机遇说明", uiRoot.transform, new Vector2(0.5f, 0.58f), new Vector2(980f, 120f), 24, TextAnchor.MiddleCenter);
            var preview = flow.GenerateOpportunityPreview();
            body.text = $"{preview.title}\n{preview.story}\n收益：{preview.reward}    风险：{preview.risk}";

            var button = CreateButton("揭开机遇", uiRoot.transform, new Vector2(0.5f, 0.42f), new Vector2(280f, 72f));
            button.onClick.AddListener(() =>
            {
                flow.ResolveOpportunity();
                BuildUi();
            });
        }

        private void BuildOpportunityChoicesPanel()
        {
            var info = CreateText("机遇说明", uiRoot.transform, new Vector2(0.5f, 0.68f), new Vector2(980f, 56f), 22, TextAnchor.MiddleCenter);
            info.text = "选择一个机遇处理方式。机遇偏收益，风险通常较小。";

            var options = flow.GenerateOpportunityOptions();
            var startX = -((options.Count - 1) * 295f) * 0.5f;
            for (var i = 0; i < options.Count; i++)
            {
                var preview = options[i];
                var label = $"{preview.title}\n{preview.story}\n收益：{preview.reward}\n风险：{preview.risk}";
                var button = CreateButton(label, uiRoot.transform, new Vector2(0.5f, 0.48f), new Vector2(280f, 190f), new Vector2(startX + i * 295f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseOpportunity(preview.templateIndex);
                    BuildUi();
                });
            }
        }

        private void BuildMysteryPanel()
        {
            var info = CreateText("神秘说明", uiRoot.transform, new Vector2(0.5f, 0.68f), new Vector2(1020f, 56f), 22, TextAnchor.MiddleCenter);
            info.text = "神秘房间会给更高收益，也可能带来惩罚战或诅咒。";

            var options = flow.GenerateMysteryOptions();
            var startX = -((options.Count - 1) * 315f) * 0.5f;
            for (var i = 0; i < options.Count; i++)
            {
                var preview = options[i];
                var label = $"{preview.title}\n{preview.story}\n收益：{preview.reward}\n风险：{preview.risk}";
                var button = CreateButton(label, uiRoot.transform, new Vector2(0.5f, 0.48f), new Vector2(300f, 205f), new Vector2(startX + i * 315f, 0f));
                button.GetComponent<Image>().color = preview.templateIndex == 1
                    ? new Color(0.34f, 0.10f, 0.10f, 0.96f)
                    : new Color(0.18f, 0.15f, 0.25f, 0.96f);
                button.onClick.AddListener(() =>
                {
                    flow.ChooseMystery(preview.templateIndex);
                    if (!flow.HasPendingNode || flow.HasPendingCardReward)
                    {
                        BuildUi();
                    }
                });
            }
        }

        private void BuildArtifactPanel()
        {
            var title = CreateText("强化标题", uiRoot.transform, new Vector2(0.5f, 0.79f), new Vector2(920f, 82f), 54, TextAnchor.MiddleCenter);
            title.text = "选择一个强化";
            title.color = new Color(1f, 0.92f, 0.76f, 0.98f);

            var info = CreateText("神器说明", uiRoot.transform, new Vector2(0.5f, 0.715f), new Vector2(1020f, 46f), 22, TextAnchor.MiddleCenter);
            info.text = "强化会立刻加入本局，改变战斗、经济或构筑节奏。";
            info.color = new Color(0.94f, 0.90f, 0.80f, 0.94f);

            var choices = flow.GenerateArtifactChoices();
            var startX = -((choices.Count - 1) * 390f) * 0.5f;
            for (var i = 0; i < choices.Count; i++)
            {
                var artifact = choices[i];
                var button = CreateArtifactChoiceCard(artifact, new Vector2(startX + i * 390f, 0f));
                button.onClick.AddListener(() =>
                {
                    flow.ChooseArtifact(artifact);
                    BuildUi();
                });
            }

            var refresh = CreateButton($"刷新\n{flow.ArtifactRerollCost} 金币", uiRoot.transform, new Vector2(0.5f, 0.16f), new Vector2(320f, 72f));
            refresh.GetComponent<Image>().color = flow.ArtifactRefreshesRemaining > 0 && flow.CurrentRun.gold >= flow.ArtifactRerollCost
                ? new Color(0.20f, 0.16f, 0.10f, 0.94f)
                : new Color(0.08f, 0.08f, 0.08f, 0.70f);
            refresh.interactable = flow.ArtifactRefreshesRemaining > 0;
            refresh.onClick.AddListener(() =>
            {
                flow.RerollArtifactChoices();
                BuildUi();
            });

            var remaining = CreateText("刷新次数", uiRoot.transform, new Vector2(0.80f, 0.16f), new Vector2(260f, 42f), 20, TextAnchor.MiddleLeft);
            remaining.text = $"剩余次数：{flow.ArtifactRefreshesRemaining}";
            remaining.color = new Color(0.92f, 0.90f, 0.82f, 0.92f);
        }

        private void BuildHeader()
        {
            var run = flow.CurrentRun;
            var panel = CreatePanel("顶部信息栏", uiRoot.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(1720f, 72f), new Color(0.015f, 0.018f, 0.022f, 0.80f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.58f, 0.45f, 0.25f, 0.32f);
            var text = CreateText("顶部信息", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(1650f, 58f), 22, TextAnchor.MiddleCenter);
            text.text = $"{GameFlowController.HeroClassName(run.heroClass)}    迷宫 {run.floor}/3 层  房间进度 {run.row}/10    金币 {run.gold}    生命 {run.playerHp:0}/{flow.PlayerMaxHpForRun():0}    本局经验 {run.heroExperience}    主角等级 {flow.CurrentRunPreviewHeroLevel()}    卡组 {run.deckCardIds.Count}    神器 {run.artifactIds.Count}\n{run.lastMessage}";
        }

        private void BuildBackdrop(Transform root)
        {
            var image = CreatePanel("背景", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.04f, 0.055f, 0.052f, 1f));
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            image.raycastTarget = false;

            var texture = Resources.Load<Texture2D>(CurrentBackdropResourcePath()) ??
                Resources.Load<Texture2D>("Art/AI/Backgrounds/battlefield_honghuang_ai") ??
                Resources.Load<Texture2D>("UI/battlefield_honghuang_ai");
            if (texture != null)
            {
                image.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                image.preserveAspect = false;
                image.color = new Color(0.56f, 0.58f, 0.60f, 1f);
            }

            var veil = CreatePanel("背景暗雾", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.0f, 0.0f, 0.0f, 0.34f));
            veil.rectTransform.offsetMin = Vector2.zero;
            veil.rectTransform.offsetMax = Vector2.zero;
            veil.raycastTarget = false;
        }

        private string CurrentBackdropResourcePath()
        {
            if (flow != null && flow.HasPendingNode && flow.PendingNode != null)
            {
                return flow.PendingNode.NodeType switch
                {
                    MapNodeType.Artifact => "Art/AI/UI/upgrade_sanctum_bg",
                    MapNodeType.Shop => "Art/AI/UI/upgrade_sanctum_bg",
                    MapNodeType.Rest => "Art/AI/UI/upgrade_sanctum_bg",
                    _ => "Art/AI/UI/map_misty_valley_bg"
                };
            }

            return flow != null && flow.HasActiveRun
                ? "Art/AI/UI/map_misty_valley_bg"
                : "Art/AI/Backgrounds/battlefield_honghuang_ai";
        }

        private void BuildSideMenu()
        {
            var panel = CreatePanel("左侧菜单", uiRoot.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(86f, 520f), new Color(0.015f, 0.018f, 0.020f, 0.58f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.70f, 0.58f, 0.35f, 0.35f);

            var entries = new (string Label, SidePanelMode Mode)[]
            {
                ("路线", SidePanelMode.None),
                ("卡组", SidePanelMode.Deck),
                ("神器", SidePanelMode.Artifacts),
                ("成长", SidePanelMode.Progress),
                ("图鉴", SidePanelMode.Codex),
                ("日志", SidePanelMode.Log)
            };
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var button = CreateButton(entry.Label, panel.transform, new Vector2(0.5f, 1f), new Vector2(68f, 70f), new Vector2(0f, -54f - i * 84f));
                button.GetComponent<Image>().color = sidePanelMode == entry.Mode
                    ? new Color(0.22f, 0.16f, 0.07f, 0.92f)
                    : new Color(0.04f, 0.05f, 0.055f, 0.62f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = 19;
                    label.resizeTextMaxSize = 19;
                }

                var mode = entry.Mode;
                button.onClick.AddListener(() =>
                {
                    sidePanelMode = mode;
                    BuildUi();
                });
            }
        }

        private void BuildSideDetailPanel()
        {
            if (sidePanelMode == SidePanelMode.None || flow == null || flow.CurrentRun == null)
            {
                return;
            }

            var panel = CreatePanel("侧边详情", uiRoot.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-260f, -12f), new Vector2(430f, 620f), new Color(0.018f, 0.022f, 0.026f, 0.82f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.70f, 0.58f, 0.35f, 0.36f);

            switch (sidePanelMode)
            {
                case SidePanelMode.Deck:
                    BuildDeckSidePanel(panel.transform);
                    break;
                case SidePanelMode.Artifacts:
                    BuildArtifactSidePanel(panel.transform);
                    break;
                case SidePanelMode.Progress:
                    BuildProgressSidePanel(panel.transform);
                    break;
                case SidePanelMode.Codex:
                    BuildCodexSidePanel(panel.transform);
                    break;
                case SidePanelMode.Log:
                    BuildRunLogSidePanel(panel.transform);
                    break;
            }
        }

        private void BuildDeckSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "当前卡组");
            var run = flow.CurrentRun;
            var grouped = run.deckCardIds
                .GroupBy(id => id)
                .Select(group => new { Id = group.Key, Count = group.Count(), Card = catalog.FindCard(group.Key) })
                .Where(entry => entry.Card != null)
                .OrderBy(entry => entry.Card.type)
                .ThenBy(entry => entry.Card.cost)
                .ThenBy(entry => entry.Card.displayName)
                .ToList();

            var typeSummary = grouped
                .GroupBy(entry => CardTypeName(entry.Card.type))
                .Select(group => $"{group.Key} {group.Sum(entry => entry.Count)}")
                .ToList();
            CreateSideText(parent, $"总数 {run.deckCardIds.Count}    可合成 {flow.UpgradableCardGroups().Count}\n{string.Join("  ", typeSummary)}", -70f, 360f, 52f, 18, TextAnchor.MiddleLeft);

            for (var i = 0; i < grouped.Count && i < 13; i++)
            {
                var entry = grouped[i];
                var y = -126f - i * 34f;
                var line = $"{entry.Card.displayName} x{entry.Count}    {CardTypeName(entry.Card.type)}  费 {entry.Card.cost}  Lv.{entry.Card.level}";
                CreateSideText(parent, line, y, 372f, 30f, 17, TextAnchor.MiddleLeft);
            }

            if (grouped.Count > 13)
            {
                CreateSideText(parent, $"还有 {grouped.Count - 13} 种卡未显示", -574f, 372f, 28f, 16, TextAnchor.MiddleCenter);
            }
        }

        private void BuildArtifactSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "本局神器");
            var run = flow.CurrentRun;
            var artifacts = run.artifactIds
                .Select(id => catalog.FindArtifact(id))
                .Where(artifact => artifact != null)
                .ToList();

            if (artifacts.Count == 0)
            {
                CreateSideText(parent, "本局还没有神器。\n优先选择神器房，可以更快形成流派。", -92f, 350f, 72f, 19, TextAnchor.MiddleCenter);
            }

            for (var i = 0; i < artifacts.Count && i < 9; i++)
            {
                var artifact = artifacts[i];
                var y = -88f - i * 54f;
                CreateSideText(parent, $"{artifact.displayName}    {ArtifactRarityName(artifact.rarity)}\n{artifact.description}", y, 372f, 48f, 16, TextAnchor.MiddleLeft);
            }

            var permanentNames = flow.PermanentProgress.permanentArtifactIds
                .Select(id => catalog.FindArtifact(id)?.displayName ?? id)
                .ToList();
            CreateSideText(parent, $"永久神器：{(permanentNames.Count == 0 ? "暂无" : string.Join("、", permanentNames))}", -560f, 372f, 44f, 17, TextAnchor.MiddleLeft);
        }

        private void BuildProgressSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "探索成长");
            var run = flow.CurrentRun;
            var progress = flow.PermanentProgress;
            var text =
                $"当前探索\n" +
                $"职业：{GameFlowController.HeroClassName(run.heroClass)}\n" +
                $"{GameFlowController.HeroClassShortStyle(run.heroClass)}\n" +
                $"层数：{run.floor}/3    房间：{run.row}/10\n" +
                $"金币：{run.gold}    生命：{run.playerHp:0}/{flow.PlayerMaxHpForRun():0}\n" +
                $"本局经验：{run.heroExperience}\n\n" +
                $"永久成长\n" +
                $"探索次数：{progress.totalRuns}\n" +
                $"通关次数：{progress.completedRuns}\n" +
                $"总经验：{progress.totalHeroExperience}\n" +
                $"主角等级：{flow.CurrentRunPreviewHeroLevel()}\n" +
                $"下级经验线：{flow.ExperienceForNextHeroLevel()}";
            CreateSideText(parent, text, -205f, 366f, 308f, 20, TextAnchor.MiddleLeft);

            var affix = $"{flow.CurrentFloorAffixName()}\n{flow.CurrentFloorAffixDescription()}";
            CreateSideText(parent, $"本层词缀\n{affix}", -500f, 366f, 92f, 18, TextAnchor.MiddleLeft);
        }

        private void BuildCodexSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "内容图鉴");
            var cardTypes = catalog.cards
                .Where(card => card != null && card.type != CardType.Curse)
                .GroupBy(card => CardTypeName(card.type))
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();
            var encounters = catalog.encounters
                .Where(encounter => encounter != null)
                .GroupBy(encounter => GameFlowController.NodeTypeName(encounter.nodeType))
                .Select(group => $"{group.Key} {group.Count()}")
                .ToList();
            var text =
                $"卡牌总数：{catalog.cards.Count(card => card != null)}\n" +
                $"{string.Join("  ", cardTypes)}\n\n" +
                $"神器总数：{catalog.artifacts.Count(artifact => artifact != null)}\n" +
                $"敌人单位：{catalog.units.Count(unit => unit != null && unit.faction == Faction.Enemy)}\n" +
                $"我方单位：{catalog.units.Count(unit => unit != null && unit.faction == Faction.Player)}\n\n" +
                $"遭遇\n{string.Join("  ", encounters)}";
            CreateSideText(parent, text, -170f, 368f, 260f, 19, TextAnchor.MiddleLeft);

            CreateSideText(parent, "提示：当前图鉴先展示内容规模，后续可以扩展为逐张卡牌、逐个怪物的详情页。", -505f, 368f, 96f, 17, TextAnchor.MiddleLeft);
        }

        private void BuildRunLogSidePanel(Transform parent)
        {
            CreateSideTitle(parent, "本局日志");
            var logs = flow.RunEventLog.Reverse().Take(13).ToList();
            if (logs.Count == 0)
            {
                CreateSideText(parent, "还没有记录。\n进入房间、战斗结算、购买卡牌和获得神器后会自动写入日志。", -106f, 360f, 96f, 18, TextAnchor.MiddleCenter);
                return;
            }

            for (var i = 0; i < logs.Count; i++)
            {
                CreateSideText(parent, logs[i], -82f - i * 38f, 374f, 34f, 15, TextAnchor.MiddleLeft);
            }
        }

        private void CreateSideTitle(Transform parent, string text)
        {
            var title = CreateText("侧栏标题", parent, new Vector2(0.5f, 1f), new Vector2(360f, 52f), 30, TextAnchor.MiddleCenter);
            title.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            title.text = text;
            title.color = new Color(1f, 0.88f, 0.52f, 0.98f);
        }

        private Text CreateSideText(Transform parent, string content, float y, float width, float height, int fontSize, TextAnchor alignment)
        {
            var text = CreateText("侧栏文字", parent, new Vector2(0.5f, 1f), new Vector2(width, height), fontSize, alignment);
            text.rectTransform.anchoredPosition = new Vector2(0f, y);
            text.text = content;
            text.color = new Color(0.94f, 0.92f, 0.84f, 0.96f);
            return text;
        }

        private void BuildDebugStrip()
        {
            if (flow == null || !flow.HasActiveRun)
            {
                return;
            }

            var panel = CreatePanel("调试条", uiRoot.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(286f, 42f), new Vector2(520f, 58f), new Color(0.015f, 0.018f, 0.020f, 0.46f));
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.60f, 0.48f, 0.28f, 0.24f);

            var buttons = new (string Label, Action Action)[]
            {
                ("+100 金", () => flow.DebugAddGold(100)),
                ("回满血", flow.DebugHealToFull),
                ("给神器", flow.DebugGrantRandomArtifact),
                ("到首领", flow.DebugJumpToBoss),
                ("最终首领", flow.DebugJumpToFinalBoss)
            };

            for (var i = 0; i < buttons.Length; i++)
            {
                var entry = buttons[i];
                var button = CreateButton(entry.Label, panel.transform, new Vector2(0f, 0.5f), new Vector2(92f, 40f), new Vector2(54f + i * 100f, 0f));
                button.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.075f, 0.78f);
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = 16;
                    label.resizeTextMaxSize = 16;
                }

                button.onClick.AddListener(() =>
                {
                    entry.Action();
                    BuildUi();
                });
            }
        }

        private Button CreateArtifactChoiceCard(ArtifactDefinition artifact, Vector2 offset)
        {
            var button = CreateButton(string.Empty, uiRoot.transform, new Vector2(0.5f, 0.45f), new Vector2(336f, 500f), offset);
            var image = button.GetComponent<Image>();
            image.color = ArtifactPanelColor(artifact.rarity);
            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = ArtifactRarityColor(artifact.rarity);
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = string.Empty;
            }

            var topSeal = AddNodeHalo(button.transform, true, true);
            topSeal.rectTransform.sizeDelta = new Vector2(78f, 78f);
            topSeal.rectTransform.anchoredPosition = new Vector2(0f, 238f);
            topSeal.color = ArtifactRarityColor(artifact.rarity);

            var iconPanel = CreatePanel("神器图底", button.transform, new Vector2(0.5f, 0.69f), new Vector2(0.5f, 0.69f), Vector2.zero, new Vector2(270f, 220f), new Color(0.02f, 0.018f, 0.014f, 0.58f));
            iconPanel.raycastTarget = false;
            AddSpriteIcon(iconPanel.transform, artifact.icon, Vector2.zero, new Vector2(210f, 180f));

            var name = CreateText("神器名", button.transform, new Vector2(0.5f, 0.40f), new Vector2(286f, 52f), 30, TextAnchor.MiddleCenter);
            name.text = artifact.displayName;
            name.color = new Color(1f, 0.94f, 0.78f, 0.98f);
            name.raycastTarget = false;

            var desc = CreateText("神器描述", button.transform, new Vector2(0.5f, 0.25f), new Vector2(276f, 106f), 22, TextAnchor.MiddleCenter);
            desc.text = artifact.description;
            desc.color = new Color(0.95f, 0.93f, 0.86f, 0.96f);
            desc.raycastTarget = false;

            var tagPanel = CreatePanel("神器分类底", button.transform, new Vector2(0.5f, 0.08f), new Vector2(0.5f, 0.08f), Vector2.zero, new Vector2(154f, 42f), WithAlpha(ArtifactRarityColor(artifact.rarity), 0.45f));
            tagPanel.raycastTarget = false;
            var tag = CreateText("神器分类", tagPanel.transform, new Vector2(0.5f, 0.5f), new Vector2(146f, 34f), 22, TextAnchor.MiddleCenter);
            tag.text = ArtifactRarityName(artifact.rarity);
            tag.color = new Color(1f, 0.94f, 0.80f, 0.98f);
            tag.raycastTarget = false;

            return button;
        }

        private Button CreateNodeButton(MapNodeRuntime node, Transform parent, Vector2 anchor, Vector2 offset)
        {
            var button = CreateButton(GameFlowController.NodeTypeName(node.NodeType), parent, anchor, new Vector2(196f, 128f), offset);
            var image = button.GetComponent<Image>();
            image.color = node.NodeType switch
            {
                MapNodeType.NormalMonster => new Color(0.19f, 0.24f, 0.22f, 0.96f),
                MapNodeType.EliteMonster => new Color(0.36f, 0.17f, 0.13f, 0.96f),
                MapNodeType.Shop => new Color(0.28f, 0.21f, 0.08f, 0.96f),
                MapNodeType.Rest => new Color(0.12f, 0.28f, 0.22f, 0.96f),
                MapNodeType.Opportunity => new Color(0.22f, 0.20f, 0.36f, 0.96f),
                MapNodeType.Mystery => new Color(0.16f, 0.13f, 0.22f, 0.96f),
                MapNodeType.Artifact => new Color(0.31f, 0.20f, 0.08f, 0.96f),
                MapNodeType.SmallBoss or MapNodeType.FinalBoss => new Color(0.42f, 0.08f, 0.08f, 0.96f),
                _ => image.color
            };
            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.rectTransform.sizeDelta = new Vector2(160f, 48f);
                label.rectTransform.anchoredPosition = new Vector2(0f, -34f);
                label.fontSize = 20;
            }

            AddSpriteIcon(button.transform, LoadNodeSprite(node.NodeType), new Vector2(0f, 22f), new Vector2(52f, 52f));
            return button;
        }

        private static Image CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Image AddSpriteIcon(Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("AI Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private static void AddTextShadow(Text text, Color color, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        private static Sprite LoadNodeSprite(MapNodeType nodeType)
        {
            var path = $"UI/Nodes/{NodeIconName(nodeType)}";
            return LoadResourceSprite(path);
        }

        private static Sprite LoadHeroClassSprite(HeroClassType heroClass)
        {
            var fileName = heroClass switch
            {
                HeroClassType.SpiritSummoner => "hero_class_spirit_summoner",
                HeroClassType.ThunderMage => "hero_class_thunder_mage",
                _ => "hero_class_border_commander"
            };
            return LoadResourceSprite($"Art/AI/UI/Classes/{fileName}") ?? LoadResourceSprite($"UI/Classes/{fileName}");
        }

        private static Sprite LoadResourceSprite(string path)
        {
            if (cachedResourceSprites.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var texture = Resources.Load<Texture2D>(path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            cachedResourceSprites[path] = sprite;
            return sprite;
        }

        private static Text CreateText(string name, Transform parent, Vector2 anchor, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            var text = go.AddComponent<Text>();
            text.font = DefaultFont();
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = fontSize;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 size)
        {
            return CreateButton(label, parent, anchor, size, Vector2.zero);
        }

        private static Button CreateButton(string label, Transform parent, Vector2 anchor, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            go.GetComponent<Image>().color = new Color(0.15f, 0.2f, 0.28f, 0.95f);

            var text = CreateText("文字", go.transform, new Vector2(0.5f, 0.5f), size - new Vector2(18f, 14f), 22, TextAnchor.MiddleCenter);
            text.text = label;
            return go.GetComponent<Button>();
        }

        private static Font DefaultFont()
        {
            cachedFont ??= UiFontProvider.DefaultFont();
            return cachedFont;
        }

        private static void EnsureEventSystem()
        {
            var eventSystem = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            }

            AddBestInputModule(eventSystem.gameObject);
        }

        private static void AddBestInputModule(GameObject eventSystem)
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModule != null)
            {
                foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                {
                    module.enabled = module.GetType() == inputSystemModule;
                }

                var inputModule = eventSystem.GetComponent(inputSystemModule);
                if (inputModule == null)
                {
                    inputModule = eventSystem.AddComponent(inputSystemModule);
                }

                inputSystemModule.GetMethod("AssignDefaultActions")?.Invoke(inputModule, null);
                return;
            }
#endif

            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
            {
                if (module is not StandaloneInputModule)
                {
                    module.enabled = false;
                }
            }

            var standalone = eventSystem.GetComponent<StandaloneInputModule>();
            if (standalone == null)
            {
                standalone = eventSystem.AddComponent<StandaloneInputModule>();
            }

            standalone.enabled = true;
        }

        private static bool IsBattleNode(MapNodeType nodeType)
        {
            return nodeType is MapNodeType.NormalMonster or MapNodeType.EliteMonster or MapNodeType.SmallBoss or MapNodeType.FinalBoss;
        }
    }
}
