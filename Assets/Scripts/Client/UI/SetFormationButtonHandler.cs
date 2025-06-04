// File: Scripts/Client/UI/SetFormationButtonHandler.cs
using Logger = Core.Logging.Logger;
using System.Collections.Generic;
using System.Linq;
using System; // For Tuple
using UnityEngine; // For Mathf

namespace Client.UI
{
    public class SetFormationButtonHandler : BaseActionButtonHandler
    {
        // This example sets a predefined "random" formation.
        // Could be expanded to cycle through formation presets or open a formation editor.
        protected override void OnButtonClicked()
        {
            var shipsInFormation = _localEscadreProxy.FormationSlots.Where(s => s.ShipEntityId.HasValue).ToList();
            if (!shipsInFormation.Any())
            {
                Logger.LogWarning($"[{GetType().Name}] No ships in formation to arrange.");
                return;
            }

            var newLayout = new List<Tuple<int, Core.Primitives.Vector2>>();
            float angleStep = 360f / shipsInFormation.Count;
            float radiusBase = 3.0f; // Slightly wider base
            float radiusIncrement = 2.0f; // Slightly more spacing

            for (int i = 0; i < shipsInFormation.Count; i++)
            {
                var slot = shipsInFormation[i];
                float currentRadius = radiusBase + (i * radiusIncrement);
                float angle = i * angleStep * Mathf.Deg2Rad;
                if (i % 2 == 1 && shipsInFormation.Count > 3) currentRadius *= 1.2f;

                newLayout.Add(new Tuple<int, Core.Primitives.Vector2>(
                    slot.ShipEntityId.Value,
                    new Core.Primitives.Vector2(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius)
                ));
            }

            if (newLayout.Any())
            {
                _clientComposer.GameActions.RequestSetFormation(newLayout);
                Logger.Log($"[{GetType().Name}] Set Formation action sent.");
            }
        }

        protected override void UpdateInteractableState()
        {
            base.UpdateInteractableState();
            if (!_button.interactable) return;

            // Enable if there's at least one ship to arrange
            if (_localEscadreProxy != null)
            {
                _button.interactable = _localEscadreProxy.FormationSlots.Any(s => s.ShipEntityId.HasValue);
            }
        }
    }
}