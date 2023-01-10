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
	static bool velocityArrowCreated = false;

	//private static ClosestApproachCalculator.T_ApproachData currentApproachData;

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

		/*if(!velocityArrowCreated)
        {
			velocityArrowCreated = true;

			CreateVelocityArrow();
		}*/

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

			/*double deltaTime = approachData.date - WorldTime.main.worldTime;
			if (((deltaTime > 0.0) && (deltaTime <60.0)) && (approachData.dist < 5000.0) )
            {
				Double2 distVector = approachData.locTarget.position - approachData.locPlayer.position;
				Double2 relSpeedVector = approachData.locTarget.velocity - approachData.locPlayer.velocity;

				VelocityArrowDrawer_OnLocationChange_Patch.setApproachPhase(distVector, relSpeedVector, deltaTime);
			}*/
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

	public static void CreateVelocityArrow()
    {
		//UnityEngine.Object listObjects1 = Object.FindObjectOfType<VelocityArrowDrawer>(true);
		/*UnityEngine.Object listObjects2 =*/
		/*Object theObject = Object.FindObjectOfType(typeof(VelocityArrowDrawer));

		if (theObject != null)
        {
			FileLog.Log("L'objet que je cherche : " + theObject.GetType().ToString()); 
		}
        else
        {
			FileLog.Log("L'objet est null!");
		}*/

		Object velocityArrowObject = Object.FindObjectOfType(typeof(VelocityArrowDrawer));

		if(velocityArrowObject != null)
		{
			if(velocityArrowObject is VelocityArrowDrawer velocityArrowComponent)
            {
				FileLog.Log("L'objet que je cherche : " + velocityArrowComponent.gameObject.ToString());

				Object transformObj = Object.Instantiate(velocityArrowComponent.gameObject.transform);

				if (transformObj is Transform transfromCopy)
				{
					Object newVelocityArrowObj = Object.Instantiate(velocityArrowComponent.gameObject, transfromCopy, true);

					if (newVelocityArrowObj is GameObject newVelocityArrow)
					{
						MonoBehaviour oldBehaviour = newVelocityArrow.GetComponent<VelocityArrowDrawer>();

						if (oldBehaviour != null)
						{
							//Object.Destroy(oldBehaviour);

							newVelocityArrow.AddComponent<CloseVelocityArrowDrawer>();
							//newVelocityArrow.SetActive(true);
							FileLog.Log("New velocity arrow: CREATED!");

							Component theCloseVelocityArrowDrawerComponent = newVelocityArrow.GetComponent<CloseVelocityArrowDrawer>();

							if (theCloseVelocityArrowDrawerComponent is CloseVelocityArrowDrawer theCloseVelocityArrowDrawer)
							{
								//theCloseVelocityArrowDrawer.safeArea = Object.Instantiate<RectTransform>(velocityArrowComponent.safeArea);
								theCloseVelocityArrowDrawer.safeArea = velocityArrowComponent.safeArea;

								theCloseVelocityArrowDrawer.velocity_X = new CloseVelocityArrowDrawer.Arrow();

								theCloseVelocityArrowDrawer.velocity_X.holder = Object.Instantiate<RectTransform>(velocityArrowComponent.velocity_X.holder);
								theCloseVelocityArrowDrawer.velocity_X.holder_Shadow = Object.Instantiate<RectTransform>(velocityArrowComponent.velocity_X.holder_Shadow);

								theCloseVelocityArrowDrawer.velocity_X.line = Object.Instantiate<Image>(velocityArrowComponent.velocity_X.line);
								theCloseVelocityArrowDrawer.velocity_X.line_Shadow = Object.Instantiate<Image>(velocityArrowComponent.velocity_X.line_Shadow);

								theCloseVelocityArrowDrawer.velocity_X.text = Object.Instantiate<Text>(velocityArrowComponent.velocity_X.text);
								theCloseVelocityArrowDrawer.velocity_X.text_Shadow = Object.Instantiate<Text>(velocityArrowComponent.velocity_X.text_Shadow);


								theCloseVelocityArrowDrawer.velocity_Y = new CloseVelocityArrowDrawer.Arrow();

								theCloseVelocityArrowDrawer.velocity_Y.holder = Object.Instantiate<RectTransform>(velocityArrowComponent.velocity_Y.holder);
								theCloseVelocityArrowDrawer.velocity_Y.holder_Shadow = Object.Instantiate<RectTransform>(velocityArrowComponent.velocity_Y.holder_Shadow);

								theCloseVelocityArrowDrawer.velocity_Y.line = Object.Instantiate<Image>(velocityArrowComponent.velocity_Y.line);
								theCloseVelocityArrowDrawer.velocity_Y.line_Shadow = Object.Instantiate<Image>(velocityArrowComponent.velocity_Y.line_Shadow);

								theCloseVelocityArrowDrawer.velocity_Y.text = Object.Instantiate<Text>(velocityArrowComponent.velocity_Y.text);
								theCloseVelocityArrowDrawer.velocity_Y.text_Shadow = Object.Instantiate<Text>(velocityArrowComponent.velocity_Y.text_Shadow);

								/*theCloseVelocityArrowDrawer.velocity_X.holder = */
								/*Object newObj = Object.Instantiate(velocityArrowComponent.velocity_X.holder);
								FileLog.Log("1");

								if (newObj is RectTransform newRectTransform)
								{
									FileLog.Log("2");
									theCloseVelocityArrowDrawer.velocity_X.holder = newRectTransform;
								}
								FileLog.Log("3");*/

								/*theCloseVelocityArrowDrawer.velocity_X.holder = velocityArrowComponent.velocity_X.holder;
								theCloseVelocityArrowDrawer.velocity_X.holder_Shadow = velocityArrowComponent.velocity_X.holder_Shadow;
								theCloseVelocityArrowDrawer.velocity_X.line = velocityArrowComponent.velocity_X.line;
								theCloseVelocityArrowDrawer.velocity_X.line_Shadow = velocityArrowComponent.velocity_X.line_Shadow;
								theCloseVelocityArrowDrawer.velocity_X.text = velocityArrowComponent.velocity_X.text;
								theCloseVelocityArrowDrawer.velocity_X.text_Shadow = velocityArrowComponent.velocity_X.text_Shadow;

								theCloseVelocityArrowDrawer.velocity_Y.holder = velocityArrowComponent.velocity_Y.holder;
								theCloseVelocityArrowDrawer.velocity_Y.holder_Shadow = velocityArrowComponent.velocity_Y.holder_Shadow;
								theCloseVelocityArrowDrawer.velocity_Y.line = velocityArrowComponent.velocity_Y.line;
								theCloseVelocityArrowDrawer.velocity_Y.line_Shadow = velocityArrowComponent.velocity_Y.line_Shadow;
								theCloseVelocityArrowDrawer.velocity_Y.text = velocityArrowComponent.velocity_Y.text;
								theCloseVelocityArrowDrawer.velocity_Y.text_Shadow = velocityArrowComponent.velocity_Y.text_Shadow;*/

								FileLog.Log("New velocity arrow: All materials copied!");
							}
							else
							{
								FileLog.Log("FAILED to get new behaviour!");
							}

						}
						else
						{
							FileLog.Log("FAILED to get old behaviour!");
						}
					}
					else
					{
						FileLog.Log("FAILED to duplicate object!");
					}
				}
				else
                {
					FileLog.Log("FAILED to duplicate transform!");
				}
			}
            else
            {
				FileLog.Log("FAILED to get convert VelocityArrowDrawer object to Component!");
			}
		}
        else
        {
			FileLog.Log("FAILED to get VelocityArrowDrawer object!");
		}
	}
}
