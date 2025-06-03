// File: Client/InputServices/TapResolverService.cs
using UnityEngine; // Fully qualify: UnityEngine.Ray, UnityEngine.Plane, UnityEngine.Vector3, Mathf
using Core.Client;
using Core.Logging;
using Core.Network.Proxies;
using Core.Primitives;
using Logger = Core.Logging.Logger;
using System.Linq;      // For Core.Primitives.Vector3, used in ResolvedTapTarget

namespace Client.InputServices
{
    public enum ResolvedTapType
    {
        NoTarget,
        AttackEscadre,
        MoveToPoint
    }

    public struct ResolvedTapTarget
    {
        public ResolvedTapType TargetType;
        public EscadreProxy.ClientProxy HitEscadreProxy; // Valid if AttackEscadre
        public Core.Primitives.Vector3 HitOceanPoint;     // Valid if MoveToPoint (using Core.Primitives.Vector3)
    }

    public static class TapResolverService
    {
        private const float SHIP_TAP_RADIUS = 2.0f;
        private static UnityEngine.Plane _oceanPlane = new UnityEngine.Plane(UnityEngine.Vector3.up, 0); // Default to Y=0

        public static ResolvedTapTarget Resolve(UnityEngine.Ray ray, ClientLevel clientLevel, EscadreProxy.ClientProxy localPlayerEscadre)
        {
            // Potentially update _oceanPlane.distance if ocean Y level is dynamic and accessible
            // For example, if OceanPresentation has a public property:
            // if (OceanPresentation.Instance != null)
            // {
            //    _oceanPlane.SetNormalAndPosition(UnityEngine.Vector3.up, new UnityEngine.Vector3(0, OceanPresentation.Instance.OceanYLevel, 0));
            // }

            EscadreProxy.ClientProxy closestHitEnemyEscadre = null;
            float closestEnemyShipHitDistance = float.MaxValue;

            if (clientLevel == null)
            {
                Logger.LogError("[TapResolverService] ClientLevel is null. Cannot resolve tap.");
                return RaycastToOceanOnly(ray);
            }

            // 1. Check for enemy ship hits
            foreach (var proxyPair in clientLevel.ActiveProxies)
            {
                if (proxyPair.Value is EscadreProxy.ClientProxy potentialEnemyEscadre &&
                    (localPlayerEscadre == null || potentialEnemyEscadre.EntityId != localPlayerEscadre.EntityId))
                {
                    if (potentialEnemyEscadre.IsDestroyed) continue; // Skip destroyed escadres

                    foreach (var slot in potentialEnemyEscadre.FormationSlots)
                    {
                        if (slot.ShipEntityId.HasValue &&
                            clientLevel.TryGetProxy(slot.ShipEntityId.Value, out var shipIProxy) && // shipIProxy is declared here
                            shipIProxy is ShipProxy.ClientProxy shipProxy) // shipProxy is declared and assigned here
                        {
                            if (shipProxy.IsDestroyed) continue; // Skip destroyed ships

                            if (RayIntersectsSphere(ray, shipProxy.Position.ToUnityVector(), SHIP_TAP_RADIUS, out float hitDist))
                            {
                                if (hitDist < closestEnemyShipHitDistance)
                                {
                                    closestEnemyShipHitDistance = hitDist;
                                    closestHitEnemyEscadre = potentialEnemyEscadre;
                                }
                            }
                        }
                    }
                }
            }

            // 2. Determine action based on enemy hit
            if (closestHitEnemyEscadre != null)
            {
                if (localPlayerEscadre == null || localPlayerEscadre.IsDestroyed)
                {
                    Logger.LogWarning("[TapResolverService] Player escadre is null or destroyed. Tapped enemy escadre will be treated as ocean tap.");
                    return RaycastToOceanOnly(ray);
                }

                bool alreadyAttackingThis = localPlayerEscadre.TargetEscadreEntityIds.Contains(closestHitEnemyEscadre.EntityId);
                if (!alreadyAttackingThis)
                {
                    return new ResolvedTapTarget { TargetType = ResolvedTapType.AttackEscadre, HitEscadreProxy = closestHitEnemyEscadre };
                }
                Logger.Log($"[TapResolverService] Already attacking Escadre {closestHitEnemyEscadre.EntityId}. Tap will fall through to ocean.");
            }

            return RaycastToOceanOnly(ray);
        }

        private static ResolvedTapTarget RaycastToOceanOnly(UnityEngine.Ray ray)
        {
            if (_oceanPlane.Raycast(ray, out float enterDistance))
            {
                UnityEngine.Vector3 unityHitPoint = ray.GetPoint(enterDistance);
                return new ResolvedTapTarget { TargetType = ResolvedTapType.MoveToPoint, HitOceanPoint = unityHitPoint.ToCoreVector() };
            }
            return new ResolvedTapTarget { TargetType = ResolvedTapType.NoTarget };
        }

        private static bool RayIntersectsSphere(UnityEngine.Ray ray, UnityEngine.Vector3 sphereCenter, float sphereRadius, out float hitDistance)
        {
            hitDistance = 0f;
            UnityEngine.Vector3 oc = ray.origin - sphereCenter;
            // Assuming ray.direction is normalized (which it should be from Camera.ScreenPointToRay)
            // float a = 1.0f; // Since ray.direction.sqrMagnitude would be 1
            float b = 2.0f * UnityEngine.Vector3.Dot(oc, ray.direction);
            float c = UnityEngine.Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            float discriminant = b * b - 4 * c; // Simplified because a=1

            if (discriminant < 0)
            {
                return false;
            }
            else
            {
                float t1 = (-b - Mathf.Sqrt(discriminant)) / 2.0f;
                float t2 = (-b + Mathf.Sqrt(discriminant)) / 2.0f;

                if (t1 >= 0 && (t1 < t2 || t2 < 0))
                {
                    hitDistance = t1;
                    return true;
                }
                if (t2 >= 0)
                {
                    hitDistance = t2;
                    return true;
                }
                return false;
            }
        }
    }
}