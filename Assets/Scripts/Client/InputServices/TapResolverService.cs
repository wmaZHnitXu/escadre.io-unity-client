// File: Client/InputServices/TapResolverService.cs
using UnityEngine; // Fully qualify: UnityEngine.Ray, UnityEngine.Plane, UnityEngine.Vector3, Mathf
using Core.Client;
using Core.Logging;
using Core.Network.Proxies;
using Core.Primitives;
using Logger = Core.Logging.Logger;
using System.Linq;

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
        private const float SHIP_TAP_RADIUS = 4.0f; // Consider making this configurable or fetched
        private static UnityEngine.Plane _oceanPlane = new UnityEngine.Plane(UnityEngine.Vector3.up, 0);

        // Call this if the ocean's Y level can change dynamically
        public static void UpdateOceanPlaneHeight(float yLevel)
        {
            _oceanPlane.SetNormalAndPosition(UnityEngine.Vector3.up, new UnityEngine.Vector3(0, yLevel, 0));
        }

        public static ResolvedTapTarget Resolve(UnityEngine.Ray ray, ClientLevel clientLevel, EscadreProxy.ClientProxy localPlayerEscadre)
        {
            if (clientLevel == null)
            {
                Logger.LogError("[TapResolverService] ClientLevel is null. Cannot resolve tap.");
                return RaycastToOceanOnly(ray);
            }

            EscadreProxy.ClientProxy identifiedEnemyEscadreForHitShip = null;
            float closestEnemyShipHitDist = float.MaxValue;
            UnityEngine.Vector3 hitPointOnEnemySurface = UnityEngine.Vector3.zero;

            // 1. Check for ship hits first
            foreach (var proxyPair in clientLevel.ActiveProxies)
            {
                if (proxyPair.Value is ShipProxy.ClientProxy potentialHitShip && !potentialHitShip.IsDestroyed)
                {
                    // Find this ship's escadre proxy
                    EscadreProxy.ClientProxy shipOwnerEscadre = clientLevel.ActiveProxies.Values
                        .OfType<EscadreProxy.ClientProxy>()
                        .FirstOrDefault(ep => !ep.IsDestroyed && ep.OwnerClientId == potentialHitShip.OwningEscadreClientId);

                    if (shipOwnerEscadre != null && (localPlayerEscadre == null || shipOwnerEscadre.EntityId != localPlayerEscadre.EntityId)) // It's an enemy escadre's ship
                    {
                        UnityEngine.Vector3 shipPositionUnity = potentialHitShip.Position.ToUnityVector();
                        if (RayIntersectsSphere(ray, shipPositionUnity, SHIP_TAP_RADIUS, out float hitDist))
                        {
                            if (hitDist < closestEnemyShipHitDist)
                            {
                                closestEnemyShipHitDist = hitDist;
                                identifiedEnemyEscadreForHitShip = shipOwnerEscadre;
                                hitPointOnEnemySurface = ray.GetPoint(hitDist);
                            }
                        }
                    }
                }
            }

            // 2. Determine action based on hit
            if (identifiedEnemyEscadreForHitShip != null)
            {
                if (localPlayerEscadre == null || localPlayerEscadre.IsDestroyed)
                {
                    Logger.LogWarning("[TapResolverService] Player escadre unavailable. Tap on enemy resolves to MoveToPoint at enemy's location.");
                    return RaycastToOceanAtPoint(hitPointOnEnemySurface);
                }

                bool alreadyAttackingThis = localPlayerEscadre.TargetEscadreEntityIds.Contains(identifiedEnemyEscadreForHitShip.EntityId);

                if (alreadyAttackingThis)
                {
                    Logger.Log($"[TapResolverService] Already attacking Escadre {identifiedEnemyEscadreForHitShip.EntityId}. Tap resolves to MoveToPoint.");
                    return RaycastToOceanAtPoint(hitPointOnEnemySurface); // Tap on already attacked enemy = move to ocean point under it
                }
                else
                {
                    return new ResolvedTapTarget { TargetType = ResolvedTapType.AttackEscadre, HitEscadreProxy = identifiedEnemyEscadreForHitShip };
                }
            }

            // 3. No enemy ship hit, raycast to ocean plane for a general move command
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

        private static ResolvedTapTarget RaycastToOceanAtPoint(UnityEngine.Vector3 surfaceHitPoint)
        {
            // Create a vertical ray from high above the surfaceHitPoint, pointing down.
            UnityEngine.Ray verticalRay = new UnityEngine.Ray(
                new UnityEngine.Vector3(surfaceHitPoint.x, surfaceHitPoint.y + 1000f, surfaceHitPoint.z),
                UnityEngine.Vector3.down
            );

            if (_oceanPlane.Raycast(verticalRay, out float enterDistanceVertical))
            {
                UnityEngine.Vector3 oceanPoint = verticalRay.GetPoint(enterDistanceVertical);
                return new ResolvedTapTarget { TargetType = ResolvedTapType.MoveToPoint, HitOceanPoint = oceanPoint.ToCoreVector() };
            }
            // Fallback: if vertical raycast fails (e.g., edge case, or ocean plane not where expected),
            // use the XZ of the surface hit and the ocean plane's Y.
            // This assumes _oceanPlane.normal is (0,1,0) or (0,-1,0). If normal is (0,1,0), plane equation is y = -_oceanPlane.distance.
            float oceanY = -_oceanPlane.distance;
            Logger.LogWarning($"[TapResolverService] Vertical raycast for ocean point under ship failed. Using fallback: XZ from ship, Y from ocean plane ({oceanY}).");
            return new ResolvedTapTarget { TargetType = ResolvedTapType.MoveToPoint, HitOceanPoint = new Core.Primitives.Vector3(surfaceHitPoint.x, oceanY, surfaceHitPoint.z) };
        }

        private static bool RayIntersectsSphere(UnityEngine.Ray ray, UnityEngine.Vector3 sphereCenter, float sphereRadius, out float hitDistance)
        {
            hitDistance = 0f;
            UnityEngine.Vector3 oc = ray.origin - sphereCenter;
            // Assuming ray.direction is normalized (which it should be from Camera.ScreenPointToRay)
            float a = 1.0f; // Since ray.direction.sqrMagnitude would be 1
            float b_half = UnityEngine.Vector3.Dot(oc, ray.direction); // B/2 term in quadratic
            float c = UnityEngine.Vector3.Dot(oc, oc) - sphereRadius * sphereRadius;
            float discriminant_quarter = b_half * b_half - a * c; // (B/2)^2 - AC

            if (discriminant_quarter < 0)
            {
                return false;
            }
            else
            {
                // t = (-B +/- sqrt(B^2 - 4AC)) / 2A
                // t = (-2*(B/2) +/- sqrt(4 * ((B/2)^2 - AC))) / 2A
                // t = (-(B/2) +/- sqrt((B/2)^2 - AC)) / A
                // Since A=1: t = -(B/2) +/- sqrt(discriminant_quarter)
                float sqrt_discriminant_quarter = Mathf.Sqrt(discriminant_quarter);
                float t1 = -b_half - sqrt_discriminant_quarter;
                float t2 = -b_half + sqrt_discriminant_quarter;

                if (t1 >= 0 && (t1 < t2 || t2 < 0)) // t1 is the closest positive intersection
                {
                    hitDistance = t1;
                    return true;
                }
                if (t2 >= 0 && t2 < t1) // t2 is the closest positive intersection (t1 was negative or further)
                {
                    hitDistance = t2;
                    return true;
                }
                // This case handles when t1 is positive and t2 is positive but t2 < t1 (which is impossible if sqrt_disc is positive)
                // Or if t1 is negative and t2 is positive.
                // Simplified logic: if t1 is positive, it's the candidate. If t1 is negative, t2 is the candidate.
                if (t1 >= 0) { hitDistance = t1; return true; }
                if (t2 >= 0) { hitDistance = t2; return true; }

                return false; // Both intersections are behind the ray origin
            }
        }
    }
}