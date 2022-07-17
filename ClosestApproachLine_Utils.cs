using SFS.World;
using SFS.World.Maps;
using System;
using UnityEngine;

// UTILITY FUNCTIONS (used for diplay purpose)
// -----------------
public class ClosestApproachLine_Utils
{
	public static string GetClosestApproachText(double approachDistance, double approachTime)
	{
		string closestApproachText = "closest approach: " + approachDistance.ToDistanceString();
		double durationBeforeApproach = approachTime - WorldTime.main.worldTime;

		// Display the remaining time 
		if ((durationBeforeApproach < 60.0) && (durationBeforeApproach > 0.0))
		{
			if (durationBeforeApproach < 1.0)
			{
				closestApproachText = closestApproachText + " (T-0s)";
			}
			else
			{
				closestApproachText = closestApproachText + " (T-" + Units.ToTimestampString(durationBeforeApproach, true, false) + ")";
			}
		}

		return closestApproachText;
	}

	public static Color GetClosestApproachLineColor(double distance)
	{
		if (distance > 100.0)
		{
			return new Color(0.627f, 0.784f, 1.0f, 0.8f); // light blue
		}
		else if (distance > 20.0)
		{
			return new Color(1.0f, 0.549f, 0.235f, 0.8f); // orange
		}
		else
		{
			return new Color(1.0f, 0.1f, 0.0f, 0.8f); // red
		}
	}

	public static void DrawDashedLine(Orbit orbit, Location start, Location end, Color color, string startText, string endText)
	{
		Vector3[] points = new Vector3[2];
		const double scaleMultiplier = 0.001;

		points[0].x = (float)(start.position.x * scaleMultiplier);
		points[0].y = (float)(start.position.y * scaleMultiplier);
		points[0].z = 0.0f;

		points[1].x = (float)(end.position.x * scaleMultiplier);
		points[1].y = (float)(end.position.y * scaleMultiplier);
		points[1].z = 0.0f;

		Map.dashedLine.DrawLine(points, orbit.Planet, color * new Color(1f, 1f, 1f, 0.5f), color * new Color(1f, 1f, 1f, 0.5f));

		Vector2 unitVector = (end.position - start.position).ToVector2.normalized;

		if (startText != null)
		{
			MapDrawer.DrawPointWithText(15, color, startText, 40, color, orbit.Planet.mapHolder.position + points[0], -unitVector, 4, 4);
		}
		if (endText != null)
		{
			MapDrawer.DrawPointWithText(15, color, endText, 40, color, orbit.Planet.mapHolder.position + points[1], unitVector, 4, 4);
		}
	}
}
