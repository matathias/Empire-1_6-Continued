using UnityEngine;

namespace FactionColonies;

/// <summary>
/// A player spot of which at most one instance of <typeparamref name="TSelf"/> is active across all
/// maps: activating one deactivates the others. Provides the active flag, its save node, the toggle
/// gizmo and the inspect-string status line; subclasses supply the texts and the side effects.
/// </summary>
public abstract class Building_ExclusiveSpot<TSelf> : Building where TSelf : Building_ExclusiveSpot<TSelf>
{
    protected bool isActive = false;

    /// <summary>Save node name of the active flag. Must never change, or existing saves lose it.</summary>
    protected abstract string ActiveSaveKey { get; }

    protected abstract string GizmoLabel { get; }
    protected abstract string GizmoDesc { get; }
    protected abstract Texture2D GizmoIcon { get; }
    protected abstract string InspectStatus { get; }

    /// <summary>Runs when this spot turns on, after the other spots were turned off.</summary>
    protected virtual void OnActivated() { }

    /// <summary>Runs when this spot is turned off directly (not when another spot replaces it).</summary>
    protected virtual void OnDeactivated() { }

    /// <summary>Runs after the player flips the gizmo; the new state is <see cref="isActive"/>.</summary>
    protected virtual void OnToggledByPlayer(bool wasActive) { }

    protected void SetActive(bool value)
    {
        if (value && !isActive)
        {
            DeactivateOtherSpots();
            OnActivated();
        }
        else if (!value && isActive)
        {
            OnDeactivated();
        }
        isActive = value;
    }

    private void DeactivateOtherSpots()
    {
        foreach (Map map in Find.Maps)
        {
            foreach (Building building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is TSelf other && other != this)
                {
                    other.isActive = false;
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref isActive, ActiveSaveKey, false);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        if (Faction == Faction.OfPlayer)
        {
            yield return new Command_Toggle
            {
                defaultLabel = GizmoLabel,
                defaultDesc = GizmoDesc,
                icon = GizmoIcon,
                isActive = () => isActive,
                toggleAction = () =>
                {
                    bool wasActive = isActive;
                    SetActive(!isActive);
                    OnToggledByPlayer(wasActive);
                }
            };
        }
    }

    public override string GetInspectString()
    {
        string baseString = base.GetInspectString();
        string statusString = InspectStatus;

        return string.IsNullOrEmpty(baseString)
            ? statusString
            : $"{baseString}\n{statusString}";
    }
}
