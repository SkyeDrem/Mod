using HarmonyLib;
using FXDSP.Core;

namespace FXDSP.Runtime
{
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.AddEntityDataWithComponents))]
    internal static class DeliveryBuiltPatch
    {
        private static void Postfix(PlanetFactory __instance, int __result)
        { DeliveryService.Active?.Register(__instance, __result); }
    }
    [HarmonyPatch(typeof(PlanetFactory), nameof(PlanetFactory.RemoveEntityWithComponents))]
    internal static class DeliveryRemovedPatch
    {
        private static void Prefix(PlanetFactory __instance, int id, out DeliveryPlatform __state)
        { __state = DeliveryService.Active?.Removing(__instance, id); }
        private static void Postfix(PlanetFactory __instance, DeliveryPlatform __state)
        { DeliveryService.Active?.RestoreCancelledRemoval(__instance, __state); }
    }
    [HarmonyPatch(typeof(PlanetTransport), nameof(PlanetTransport.SetStationStorage))]
    internal static class DeliveryStationSlotPatch
    {
        private static bool Prefix(PlanetTransport __instance, int stationId, int storageIdx, int itemId, ref int itemCountMax)
        { return DeliveryService.Active == null || DeliveryService.Active.PrepareStationSlot(__instance, stationId, storageIdx, itemId, ref itemCountMax); }
        private static void Postfix(PlanetTransport __instance, int stationId, int storageIdx)
        {
            if (stationId > 0 && stationId < __instance.stationPool.Length)
                DeliveryService.Active?.StationSlotChanged(__instance.stationPool[stationId], storageIdx);
        }
    }
    [HarmonyPatch(typeof(UIStationStorage), "OnItemPickerReturn")]
    internal static class DeliveryStationPickerPatch
    {
        private static bool Prefix(UIStationStorage __instance, ItemProto itemProto)
        {
            var service = DeliveryService.Active; DeliveryPlatform platform;
            if (service == null || !service.Find(__instance.station, out platform)) return true;
            if (itemProto == null || __instance.index < 0 || __instance.index >= __instance.station.storage.Length) return false;
            if (__instance.station.storage[__instance.index].itemId == itemProto.ID) return false;
            var transport = __instance.stationWindow.factory.transport;
            int maximum = DeliveryBuildings.NativeBuffer;
            if (!service.PrepareStationSlot(transport, __instance.station.id, __instance.index, itemProto.ID, ref maximum)) return false;
            transport.SetStationStorage(__instance.station.id, __instance.index, itemProto.ID, maximum,
                (ELogisticStorage)1, platform.ProtoId == DeliveryBuildings.Interstellar ? (ELogisticStorage)1 : (ELogisticStorage)0, GameMain.mainPlayer);
            return false;
        }
    }
    [HarmonyPatch(typeof(UIStationStorage), "_OnUpdate")]
    internal static class DeliveryStationDisplayPatch
    {
        private static void Postfix(UIStationStorage __instance)
        {
            var service = DeliveryService.Active; DeliveryPlatform platform;
            if (service == null || !service.Find(__instance.station, out platform)) return;
            int index = __instance.index;
            if (index < 0 || index >= __instance.station.storage.Length) return;
            var slot = __instance.station.storage[index]; DeliveryStock reserve;
            var count = new System.Numerics.BigInteger(slot.count);
            bool includeReserve = service.SlotBound(platform, index, slot.itemId);
            for (int previous = 0; previous < index; previous++)
                if (__instance.station.storage[previous].itemId == slot.itemId) includeReserve = false;
            if (includeReserve && platform.Reserve.TryGetValue(slot.itemId, out reserve)) count += reserve.Count;
            __instance.countValueText.text = count.ToString("N0");
            __instance.maxValueText.text = service.SlotBound(platform, index, slot.itemId) ? "∞" : TextLocalization.Translate("重复设置");
        }
    }
}
