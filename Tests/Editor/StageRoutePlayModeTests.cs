using System.Collections;
using System.Reflection;
using AIBuilder.CreatureStage;
using AIBuilder.EditorTools.CreatureStage;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AIBuilder.EditorTools.Tests
{
    public sealed class StageRoutePlayModeTests
    {
        private static void Refresh()
        {
            var type = typeof(StageRouteOverlay);
            type.GetField("nextRefresh", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, 0d);
            type.GetMethod("RefreshLines", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }

        [UnityTest]
        public IEnumerator RouteDisplaySurvivesPlayModeWithoutMovingPoints()
        {
            var enabled = EditorSettings.enterPlayModeOptionsEnabled;
            var options = EditorSettings.enterPlayModeOptions;
            var go = new GameObject("OverlayPlayModeRegression");
            var route = go.AddComponent<StageRoute>();
            route.autoConnectPrevious = false;
            route.autoBindGround = false;
            route.snapToGround = false;
            route.smoothConnections = false;
            route.points = new[] { Vector3.zero, new Vector3(4, 2, 7) };
            go.AddComponent<StageRouteLine>();
            var line = go.GetComponent<LineRenderer>();
            try
            {
                // 保留静态缓存和场景对象，验证最容易漏掉恢复的快速进入运行模式。
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
                StageRouteOverlay.RefreshEditedRoute(route);
                Assert.That(StageRouteOverlay.TryDisplayEnd(route, out var before), Is.True);
                line.positionCount = 0;
                yield return new EnterPlayMode(false);
                Refresh();
                Assert.That(StageRouteOverlay.TryDisplayEnd(route, out var during), Is.True, "运行中也应建立 Scene 线和节点缓存");
                Assert.That(during, Is.EqualTo(before));
                Assert.That(line.positionCount, Is.GreaterThan(0));
                yield return new ExitPlayMode();
                yield return null;
                go = GameObject.Find("OverlayPlayModeRegression");
                Assert.That(go, Is.Not.Null, "退出后编辑场景对象应被恢复");
                route = go.GetComponent<StageRoute>();
                line = go.GetComponent<LineRenderer>();
                Refresh();
                Assert.That(StageRouteOverlay.TryDisplayEnd(route, out var after), Is.True, "退出后无需移动节点");
                Assert.That(after, Is.EqualTo(before));
                Assert.That(line.positionCount, Is.GreaterThan(0));
                var sampled = StageRouteOverlay.SampledRouteCount;
                Refresh();
                Assert.That(StageRouteOverlay.SampledRouteCount, Is.EqualTo(sampled), "静止时继续复用缓存");
            }
            finally
            {
                EditorSettings.enterPlayModeOptionsEnabled = enabled;
                EditorSettings.enterPlayModeOptions = options;
                Object.DestroyImmediate(go);
            }
        }
    }
}
