using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using UnityEngine;
using static TheOtherRoles.TheOtherRoles;

namespace TheOtherRoles.Patches
{
    [Harmony]
    public class AdminPatch
    {
        static Dictionary<SystemTypes, List<Color>> playerColors = new();
        static float adminTimer = 0f;
        static TMPro.TextMeshPro OutOfTime;
        static TMPro.TextMeshPro TimeRemaining;
        static bool clearedIcons = false;

        static PlainShipRoom room;
        static List<SystemTypes> filterCockpitAdmin = new(){
            SystemTypes.Cockpit,
            SystemTypes.Armory,
            SystemTypes.Kitchen,
            SystemTypes.VaultRoom,
            SystemTypes.Comms
        };
        static List<SystemTypes> filterRecordsAdmin = new(){
            SystemTypes.Records,
            SystemTypes.Lounge,
            SystemTypes.CargoBay,
            SystemTypes.Showers,
            SystemTypes.Ventilation
        };

        private static bool filterAdmin(SystemTypes type)
        {
            // イビルハッカーのアドミンは今まで通り
            if (PlayerControl.LocalPlayer.isRole(RoleType.EvilHacker)) return true;

            if (CustomOptionHolder.airshipRestrictedAdmin.getBool())
            {
                if (room.name == "Cockpit" && !filterCockpitAdmin.Contains(type))
                {
                    return false;
                }
                if (room.name == "Records" && !filterRecordsAdmin.Contains(type))
                {
                    return false;
                }
            }
            return true;
        }


        public static void ResetData()
        {
            adminTimer = 0f;
            if (TimeRemaining != null)
            {
                UnityEngine.Object.Destroy(TimeRemaining);
                TimeRemaining = null;
            }

            if (OutOfTime != null)
            {
                UnityEngine.Object.Destroy(OutOfTime);
                OutOfTime = null;
            }
        }

        static void UseAdminTime()
        {
            // Don't waste network traffic if we're out of time.
            if (MapOptions.restrictDevices > 0 && MapOptions.restrictAdmin && MapOptions.restrictAdminTime > 0f && PlayerControl.LocalPlayer.isAlive() &&
                    !(PlayerControl.LocalPlayer.isRole(RoleType.EvilHacker) || PlayerControl.LocalPlayer.isRole(RoleType.MimicA)))
            {
                MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.UseAdminTime, Hazel.SendOption.Reliable, -1);
                writer.Write(adminTimer);
                AmongUsClient.Instance.FinishRpcImmediately(writer);
                RPCProcedure.useAdminTime(adminTimer);
            }
            adminTimer = 0f;
        }

        [HarmonyPatch(typeof(MapConsole), nameof(MapConsole.CanUse))]
        public static class MapConsoleCanUsePatch
        {
            public static bool Prefix(ref float __result, MapConsole __instance, [HarmonyArgument(0)] GameData.PlayerInfo pc, [HarmonyArgument(1)] out bool canUse, [HarmonyArgument(2)] out bool couldUse)
            {
                canUse = couldUse = false;
                return true;
            }
        }

        [HarmonyPatch(typeof(MapConsole), nameof(MapConsole.Use))]
        public static class MapConsoleUsePatch
        {
            public static bool Prefix(MapConsole __instance)
            {
                return MapOptions.canUseAdmin;
            }
        }

        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.OnEnable))]
        class MapCountOverlayOnEnablePatch
        {
            static void Prefix(MapCountOverlay __instance)
            {
                adminTimer = 0f;

                // 現在地からどのアドミンを使っているか特定する
                PlainShipRoom[] array = null;
                UnhollowerBaseLib.Il2CppReferenceArray<Collider2D> buffer = new Collider2D[10];
                ContactFilter2D filter = default(ContactFilter2D);
                filter.layerMask = Constants.PlayersOnlyMask;
                filter.useLayerMask = true;
                filter.useTriggers = false;
                if (ShipStatus.Instance)
                {
                    array = ShipStatus.Instance.AllRooms;
                }
                foreach (PlainShipRoom plainShipRoom in array)
                {
                    if (plainShipRoom.roomArea)
                    {
                        int hitCount = plainShipRoom.roomArea.OverlapCollider(filter, buffer);
                        if (hitCount == 0) continue;
                        for (int i = 0; i < hitCount; i++)
                        {
                            if (buffer[i].gameObject == PlayerControl.LocalPlayer.gameObject)
                            {
                                room = plainShipRoom;
                                break;
                            }
                        }
                        if (room != null) break;
                    }
                }
            }
        }


        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.OnDisable))]
        class MapCountOverlayOnDisablePatch
        {
            static void Prefix(MapCountOverlay __instance)
            {
                UseAdminTime();
            }
        }

        [HarmonyPatch(typeof(MapCountOverlay), nameof(MapCountOverlay.Update))]
        class MapCountOverlayUpdatePatch
        {

            static bool Prefix(MapCountOverlay __instance)
            {
                adminTimer += Time.deltaTime;
                if (adminTimer > 0.1f)
                    UseAdminTime();

                // Save colors for the Hacker
                __instance.timer += Time.deltaTime;
                if (__instance.timer < 0.1f)
                {
                    return false;
                }
                __instance.timer = 0f;

                playerColors = new Dictionary<SystemTypes, List<Color>>();

                if (MapOptions.restrictDevices > 0 && MapOptions.restrictAdmin)
                {
                    if (OutOfTime == null)
                    {
                        OutOfTime = UnityEngine.Object.Instantiate(__instance.SabotageText, __instance.SabotageText.transform.parent);
                        OutOfTime.text = ModTranslation.getString("restrictOutOfTime");
                    }

                    if (TimeRemaining == null)
                    {
                        TimeRemaining = UnityEngine.Object.Instantiate(HudManager.Instance.TaskText, __instance.transform);
                        TimeRemaining.alignment = TMPro.TextAlignmentOptions.BottomRight;
                        TimeRemaining.transform.position = Vector3.zero;
                        TimeRemaining.transform.localPosition = new Vector3(3.25f, 5.25f);
                        TimeRemaining.transform.localScale *= 2f;
                        TimeRemaining.color = Palette.White;
                    }

                    if (PlayerControl.LocalPlayer.isRole(RoleType.EvilHacker) || PlayerControl.LocalPlayer.isRole(RoleType.MimicA))
                    {
                        TimeRemaining.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (MapOptions.restrictAdminTime <= 0f)
                        {
                            __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
                            OutOfTime.gameObject.SetActive(true);
                            TimeRemaining.gameObject.SetActive(false);
                            if (clearedIcons == false)
                            {
                                foreach (CounterArea ca in __instance.CountAreas) ca.UpdateCount(0);
                                clearedIcons = true;
                            }
                            return false;
                        }

                        clearedIcons = false;
                        OutOfTime.gameObject.SetActive(false);
                        string timeString = TimeSpan.FromSeconds(MapOptions.restrictAdminTime).ToString(@"mm\:ss\.ff");
                        TimeRemaining.text = String.Format(ModTranslation.getString("timeRemaining"), timeString);
                        //TimeRemaining.color = MapOptions.restrictAdminTime > 10f ? Palette.AcceptedGreen : Palette.ImpostorRed;
                        TimeRemaining.gameObject.SetActive(true);
                    }
                }

                bool commsActive = false;
                foreach (PlayerTask task in PlayerControl.LocalPlayer.myTasks)
                    if (task.TaskType == TaskTypes.FixComms) commsActive = true;

                if (!__instance.isSab && commsActive)
                {
                    __instance.isSab = true;
                    __instance.BackgroundColor.SetColor(Palette.DisabledGrey);
                    __instance.SabotageText.gameObject.SetActive(true);
                    OutOfTime.gameObject.SetActive(false);
                    return false;
                }

                if (__instance.isSab && !commsActive)
                {
                    __instance.isSab = false;
                    __instance.BackgroundColor.SetColor(Color.green);
                    __instance.SabotageText.gameObject.SetActive(false);
                    OutOfTime.gameObject.SetActive(false);
                }

                for (int i = 0; i < __instance.CountAreas.Length; i++)
                {
                    CounterArea counterArea = __instance.CountAreas[i];
                    List<Color> roomColors = new();
                    playerColors.Add(counterArea.RoomType, roomColors);

                    if (!commsActive && counterArea.RoomType > SystemTypes.Hallway)
                    {
                        PlainShipRoom plainShipRoom = ShipStatus.Instance.FastRooms[counterArea.RoomType];

                        if (plainShipRoom != null && plainShipRoom.roomArea)
                        {
                            int num = plainShipRoom.roomArea.OverlapCollider(__instance.filter, __instance.buffer);
                            int num2 = num;

                            // ロミジュリと絵画の部屋をアドミンの対象から外す
                            if (CustomOptionHolder.airshipOldAdmin.getBool() && (counterArea.RoomType == SystemTypes.Ventilation || counterArea.RoomType == SystemTypes.HallOfPortraits))
                            {
                                num2 = 0;
                            }

                            // アドミン毎に表示する範囲を制限する
                            if (!filterAdmin(counterArea.RoomType))
                            {
                                num2 = 0;
                            }

                            for (int j = 0; j < num; j++)
                            {
                                Collider2D collider2D = __instance.buffer[j];
                                if (!(collider2D.tag == "DeadBody"))
                                {
                                    PlayerControl component = collider2D.GetComponent<PlayerControl>();
                                    if (!component || component.Data == null || component.Data.Disconnected || component.Data.IsDead)
                                    {
                                        num2--;
                                    }
                                    else if (component.isRole(RoleType.Puppeteer) && Puppeteer.stealthed)
                                    {
                                        num2--;
                                    }
                                    else if (component == Puppeteer.dummy && !Puppeteer.stealthed)
                                    {
                                        num2--;
                                    }
                                    else if (component?.cosmetics?.currentBodySprite?.BodySprite.material != null)
                                    {
                                        // Color color = component.myRend.material.GetColor("_BodyColor");
                                        Color color = Palette.PlayerColors[component.Data.DefaultOutfit.ColorId];
                                        if (Hacker.onlyColorType)
                                        {
                                            var id = Mathf.Max(0, Palette.PlayerColors.IndexOf(color));
                                            color = Helpers.isLighterColor((byte)id) ? Palette.PlayerColors[7] : Palette.PlayerColors[6];
                                        }
                                        roomColors.Add(color);
                                    }
                                }
                                else
                                {
                                    DeadBody component = collider2D.GetComponent<DeadBody>();
                                    if (component)
                                    {
                                        GameData.PlayerInfo playerInfo = GameData.Instance.GetPlayerById(component.ParentId);
                                        if (playerInfo != null)
                                        {
                                            var color = Palette.PlayerColors[playerInfo.Object.CurrentOutfit.ColorId];
                                            if (Hacker.onlyColorType)
                                                color = Helpers.isLighterColor(playerInfo.Object.CurrentOutfit.ColorId) ? Palette.PlayerColors[7] : Palette.PlayerColors[6];
                                            roomColors.Add(color);
                                        }
                                    }
                                }
                            }
                            counterArea.UpdateCount(num2);
                        }
                        else
                        {
                            Debug.LogWarning("Couldn't find counter for:" + counterArea.RoomType);
                        }
                    }
                    else
                    {
                        counterArea.UpdateCount(0);
                    }
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(CounterArea), nameof(CounterArea.UpdateCount))]
        class CounterAreaUpdateCountPatch
        {
            private static Material defaultMat;
            private static Material newMat;
            static void Postfix(CounterArea __instance)
            {
                // Hacker display saved colors on the admin panel
                bool showHackerInfo = Hacker.hacker != null && Hacker.hacker == PlayerControl.LocalPlayer && Hacker.hackerTimer > 0;
                if (playerColors.ContainsKey(__instance.RoomType))
                {
                    List<Color> colors = playerColors[__instance.RoomType];
                    List<Color> impostorColors = new();
                    List<Color> mimicKColors = new();
                    List<Color> deadBodyColors = new();
                    foreach (var p in PlayerControl.AllPlayerControls)
                    {
                        // var color = p.myRend.material.GetColor("_BodyColor");
                        var color = Palette.PlayerColors[p.Data.DefaultOutfit.ColorId];
                        if (p.isImpostor())
                        {
                            impostorColors.Add(color);
                            if (p.isRole(RoleType.MimicK))
                            {
                                mimicKColors.Add(color);
                            }
                        }
                        else if (p.isDead())
                        {
                            deadBodyColors.Add(color);
                        }
                    }

                    for (int i = 0; i < __instance.myIcons.Count; i++)
                    {
                        PoolableBehavior icon = __instance.myIcons[i];
                        SpriteRenderer renderer = icon.GetComponent<SpriteRenderer>();

                        if (renderer != null)
                        {
                            if (defaultMat == null) defaultMat = renderer.material;
                            if (newMat == null) newMat = UnityEngine.Object.Instantiate<Material>(defaultMat);
                            if (showHackerInfo && colors.Count > i)
                            {
                                renderer.material = newMat;
                                var color = colors[i];
                                renderer.material.SetColor("_BodyColor", color);
                                var id = Palette.PlayerColors.IndexOf(color);
                                if (id < 0)
                                {
                                    renderer.material.SetColor("_BackColor", color);
                                }
                                else
                                {
                                    renderer.material.SetColor("_BackColor", Palette.ShadowColors[id]);
                                }
                                renderer.material.SetColor("_VisorColor", Palette.VisorColor);
                            }
                            else if ((PlayerControl.LocalPlayer.isRole(RoleType.EvilHacker) && EvilHacker.canHasBetterAdmin) || PlayerControl.LocalPlayer.isRole(RoleType.MimicA))
                            {
                                renderer.material = newMat;
                                var color = colors[i];
                                if (impostorColors.Contains(color))
                                {
                                    if (mimicKColors.Contains(color))
                                    {
                                        color = Palette.PlayerColors[3];
                                    }
                                    else
                                    {
                                        color = Palette.ImpostorRed;
                                    }
                                    renderer.material.SetColor("_BodyColor", color);
                                    var id = Palette.PlayerColors.IndexOf(color);
                                    if (id < 0)
                                    {
                                        renderer.material.SetColor("_BackColor", color);
                                    }
                                    else
                                    {
                                        renderer.material.SetColor("_BackColor", Palette.ShadowColors[id]);
                                    }
                                    renderer.material.SetColor("_VisorColor", Palette.VisorColor);
                                }
                                else if (deadBodyColors.Contains(color))
                                {
                                    color = Palette.Black;
                                    renderer.material.SetColor("_BodyColor", color);
                                    var id = Palette.PlayerColors.IndexOf(color);
                                    if (id < 0)
                                    {
                                        renderer.material.SetColor("_BackColor", color);
                                    }
                                    else
                                    {
                                        renderer.material.SetColor("_BackColor", Palette.ShadowColors[id]);
                                    }
                                    renderer.material.SetColor("_VisorColor", Palette.VisorColor);
                                }
                                else
                                {
                                    renderer.material = defaultMat;
                                }
                            }
                            else
                            {
                                renderer.material = defaultMat;
                            }
                        }
                    }
                }
            }
        }
    }
}
