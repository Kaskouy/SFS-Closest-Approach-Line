using HarmonyLib;
using SFS.World;
using SFS.World.Maps;
using UnityEngine;
using UnityEngine.UI;


// The 2 following patches allow to reset the latest approach data when the player changes the target
// --------------------------------------------------------------------------------------------------
[HarmonyPatch(typeof(MapNavigation), nameof(MapNavigation.SetTarget))]
public class MapNavigation_SetTarget_Patch
{
	[HarmonyPostfix]
	public static void SetTarget_postfix()
    {
		ClosestApproachCalculator.resetLastApproachDataValidity();
		ClosestApproachCalculator.resetLastApproachDataValidity_MultiTurn();
	}
}

[HarmonyPatch(typeof(MapNavigation), nameof(MapNavigation.ToggleTarget))]
public class MapNavigation_ToggleTarget_Patch
{
	[HarmonyPostfix]
	public static void ToggleTarget_postfix()
	{
		ClosestApproachCalculator.resetLastApproachDataValidity();
		ClosestApproachCalculator.resetLastApproachDataValidity_MultiTurn();
	}
}

// This patch allows to reset the latest approach data when the player switches to another ship
// --------------------------------------------------------------------------------------------
[HarmonyPatch(typeof(MapNavigation), "OnPlayerChange")]
public class MapNavigation_OnPlayerChange_Patch
{
	[HarmonyPostfix]
	public static void OnPlayerChange_postfix(Player newPlayer)
	{
		ClosestApproachCalculator.setPlayer(newPlayer);
		ClosestApproachCalculator.resetLastApproachDataValidity();
		ClosestApproachCalculator.resetLastApproachDataValidity_MultiTurn();
	}
}


// Patch for the DrawNavigation method: calculates and display the closest approach line
// -------------------------------------------------------------------------------------
[HarmonyPatch(typeof(MapNavigation), nameof(MapNavigation.DrawNavigation))]
public class MapNavigation_Patch
{
	[HarmonyPostfix]
	public static void DrawNavigation_postfix(MapNavigation __instance, SelectableObject ___target)
    {
		Player value = PlayerController.main.player.Value;
		if (value == null || value.mapPlayer == null)
		{
			return;
		}

		if (___target == null)
		{
			return; // no target
		}

		if ( !GetOrbits(value.mapPlayer.Trajectory, out var orbits2) || !GetOrbits(___target.Trajectory, out var orbits3) )
		{
			return;
		}

		Orbit targetOrbit = orbits3[0];
		Orbit playerOrbit = null;

		// Search a trajectory that makes an encounter possible with the target
		// --------------------------------------------------------------------
		for(int i_orbit = 0; i_orbit < orbits2.Length; i_orbit++)
        {
			// search for the first trajectory that orbits the same body as target
			if( orbits2[i_orbit].Planet == targetOrbit.Planet) 
            {
				// check if the player object doesn't already have an encounter planned with the targeted object (if a planet is targeted...)
				if( !((i_orbit < orbits2.Length - 1) && (orbits2[i_orbit+1].Planet.mapPlanet == ___target)) )
                {
					playerOrbit = orbits2[i_orbit];
					break;
				}
            }
        }

		if( playerOrbit == null )
        {
			return;
        }

		// Calculate the closest approach
		// ------------------------------
		ClosestApproachCalculator.T_ApproachData approachData = ClosestApproachCalculator.CalculateClosestApproach(playerOrbit, targetOrbit);

		if (approachData.validity)
		{
			// If a valid approach is found, display it
			// ----------------------------------------
			Color lineColor = ClosestApproachLine_Utils.GetClosestApproachLineColor(approachData.dist);
			string closestApproachText = ClosestApproachLine_Utils.GetClosestApproachText(approachData.dist, ClosestApproachCalculator.GetApproachSpeed(approachData), approachData.date);

			ClosestApproachLine_Utils.DrawDashedLine(playerOrbit, approachData.locPlayer, approachData.locTarget, lineColor, null, closestApproachText);
		}

		// Evolution: calculate closest approach on several turns
		// ------------------------------------------------------
		uint nbTurns1 = 0, nbTurns2 = 0;
		ClosestApproachCalculator.T_ApproachData approachData_multi1, approachData_multi2;
		ClosestApproachCalculator.CalculateClosestApproach_MultiTurn(playerOrbit, targetOrbit, approachData, out approachData_multi1, out nbTurns1, out approachData_multi2, out nbTurns2);

		Color lighGreen = new Color(0.706f, 1.0f, 0.902f, 0.8f); // light green

		// Show approach line for node 1
		if (approachData_multi1.validity && nbTurns1 > 1) // don't show it if nbTurns is 1, would be redundant with classic closest approach line
        {
			string closestApproachText = "Best approach: " + approachData_multi1.dist.ToDistanceString() + " (" + nbTurns1 + " turns)";

			ClosestApproachLine_Utils.DrawDashedLine(playerOrbit, approachData_multi1.locPlayer, approachData_multi1.locTarget, lighGreen, null, closestApproachText);
		}
		/*else if(approachData_multi1.validity)
        {
			FileLog.Log("Approach1 on next turn");
        }*/

		// Show approach line for node 2
		if (approachData_multi2.validity && nbTurns2 > 1) // don't show it if nbTurns is 1, would be redundant with classic closest approach line
		{
			string closestApproachText = "Best approach: " + approachData_multi2.dist.ToDistanceString() + " (" + nbTurns2 + " turns)";

			ClosestApproachLine_Utils.DrawDashedLine(playerOrbit, approachData_multi2.locPlayer, approachData_multi2.locTarget, lighGreen, null, closestApproachText);
		}
		/*else if (approachData_multi2.validity)
		{
			FileLog.Log("Approach2 on next turn");
		}*/

		

		
	}


	// copy of the GetOrbits method in the original file
	static bool GetOrbits(Trajectory a, out Orbit[] orbits)
	{
		orbits = new Orbit[a.paths.Count];
		for (int i = 0; i < a.paths.Count; i++)
		{
			if (!(a.paths[i] is Orbit orbit3))
			{
				return false;
			}
			orbits[i] = orbit3;
		}
		return orbits.Length != 0;
	}
}
