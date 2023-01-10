using HarmonyLib;
using SFS.World;
using SFS.World.Maps;
using UnityEngine;
using UnityEngine.UI;
using SFS.UI;
using System;



[HarmonyPatch(typeof(VelocityArrowDrawer), "OnLocationChange")]
class VelocityArrowDrawer_OnLocationChange_Patch
{
	public static bool isInApproachPhase = false;
	public static bool isInNeighborhoodMode = false;

	public static Double2 distance; // used in neighborhood mode only
	public static Double2 closestDistance; // used in final approach mode only
	public static Double2 relativeSpeed; // used in both modes
	public static double remainingTime; // used in final approach mode only

	public static Color defaultArrowColor = new Color(1.0f, 1.0f, 1.0f, 0.9019608f);

	public static DynamicColor dynamicColorVelocityArrow = null;
	public static DynamicColor dynamicColorVelocityText = null;
	public static DynamicColor dynamicColorImpactArrow;
	public static DynamicColor dynamicColorImpactText;

	public static Color closestDistanceColorArrow = new Color(1.000f, 0.235f, 0.235f, 0.6f);
	public static Color closestDistanceColorText = new Color(1.000f, 0.235f, 0.235f);

	public static Color distanceColorArrow = new Color(0.314f, 1.000f, 0.627f, 0.6f);
	public static Color distanceColorText = new Color(0.314f, 1.000f, 0.627f);

	private const double C_SEARCH_CLOSEST_APPROACH_PERIOD = 60.0;
	private const double C_NEIGHBORHOOD_DISTANCE = 10000.0;

	public static string getVelocityString(Color color)
    {
		double speed = relativeSpeed.magnitude;

		bool showDecimals = (speed < 10.0) ? true : false;
		string speedText = Units.ToVelocityString(speed, showDecimals);

		string label = "ΔV = " + speedText;
		return label;
	}

	public static string getClosestDistanceString(Color color)
    {
		float dist = (float)closestDistance.magnitude;

		bool showDecimals = (dist < 100.0) ? true : false;
		string distText = Units.ToDistanceString(dist, showDecimals);

		string str_remainingSeconds;

		if (remainingTime > 1.0)
		{
			str_remainingSeconds = Units.ToTimestampString(remainingTime, true, false);
		}
		else
		{
			str_remainingSeconds = "0s";
		}

		string label = "Closest approach = " + distText + " (T-" + str_remainingSeconds + ")";

		return label;
	}

	public static string getImpactString(Color color)
    {
		double speed = relativeSpeed.magnitude;

		string str_remainingSeconds;

		if (remainingTime > 1.0)
		{
			str_remainingSeconds = Units.ToTimestampString(remainingTime, true, false);
		}
		else
		{
			str_remainingSeconds = "0s";
		}

		string label;

		if(speed > 5.0)
        {
			label = "IMPACT!! (T-" + str_remainingSeconds + ")";
		}
		else
        {
			label = "Encounter (T-" + str_remainingSeconds + ")";
		}

		return label;
	}

	public static string getDistanceString(Color color)
	{
		float dist = (float)distance.magnitude;

		bool showDecimals = (dist < 100.0) ? true : false;
		string distText = Units.ToDistanceString(dist, showDecimals);

		string label = "Distance = " + distText;

		return label;
	}

	public static void resetApproachPhase()
    {
		if (isInApproachPhase)
        {
			VelocityArrowDrawer theVelocityArrow = UnityEngine.Object.FindObjectOfType(typeof(VelocityArrowDrawer)) as VelocityArrowDrawer;
			setVelocityArrowColor(theVelocityArrow.velocity_X, defaultArrowColor, defaultArrowColor);
			setVelocityArrowColor(theVelocityArrow.velocity_Y, defaultArrowColor, defaultArrowColor);

			dynamicColorVelocityArrow = null;
			dynamicColorVelocityText = null;
		}

		isInApproachPhase = false;
	}

	public static void setApproachPhase(Double2 closestDist, Double2 relativeVel, double remainingTime)
    {
		VelocityArrowDrawer_OnLocationChange_Patch.closestDistance = closestDist;
		VelocityArrowDrawer_OnLocationChange_Patch.relativeSpeed = relativeVel;
		VelocityArrowDrawer_OnLocationChange_Patch.remainingTime = remainingTime;
		if (!isInApproachPhase)
		{
			if (dynamicColorVelocityArrow == null) dynamicColorVelocityArrow = new DynamicColor(DynamicColor.E_COLOR.LIGHT_BLUE_2, DynamicColor.E_BLINKING_TYPE.BLINKING_SMOOTH, 1.8f);
			if (dynamicColorVelocityText == null) dynamicColorVelocityText = new DynamicColor(DynamicColor.E_COLOR.LIGHT_BLUE, DynamicColor.E_BLINKING_TYPE.BLINKING_SMOOTH, 1.8f);
			dynamicColorImpactArrow   = new DynamicColor(DynamicColor.E_COLOR.RED_2, DynamicColor.E_BLINKING_TYPE.BLINKING_BINARY, 1.2f);
			dynamicColorImpactText = new DynamicColor(DynamicColor.E_COLOR.RED, DynamicColor.E_BLINKING_TYPE.BLINKING_BINARY, 1.2f);
		}
		isInApproachPhase = true;

		isInNeighborhoodMode = false;
	}

	private static void setVelocityArrowColor(VelocityArrowDrawer.Arrow arrow, Color arrowColor, Color textColor)
    {
		arrow.line.color = arrowColor;
		arrow.text.color = textColor;

		try
        {
			// That code will work as long as Stef doesn't modify the transforms (hence why it's in a try catch)
			arrow.line.transform.parent.GetChild(0).gameObject.GetComponent<Image>().color = arrowColor; // Corresponds to Arrow X: tip of the arrow
			arrow.line.transform.GetChild(1).gameObject.GetComponent<Image>().color = arrowColor; // Corresponds to Base X: origin of the arrow
		}
		catch (Exception e)
        {
			// nothing...
        }
	}

	public static bool isInNeighborhood(Location location)
    {
		if (Map.navigation.target == null)
		{
			return false;
		}

		distance = Map.navigation.target.Location.position - location.position;

		if (distance.magnitude < C_NEIGHBORHOOD_DISTANCE )
        {
			relativeSpeed = Map.navigation.target.Location.velocity - location.velocity;

			if(!isInNeighborhoodMode)
            {
				if (dynamicColorVelocityArrow == null) dynamicColorVelocityArrow = new DynamicColor(DynamicColor.E_COLOR.LIGHT_BLUE_2, DynamicColor.E_BLINKING_TYPE.BLINKING_SMOOTH, 1.8f);
				if (dynamicColorVelocityText  == null) dynamicColorVelocityText  = new DynamicColor(DynamicColor.E_COLOR.LIGHT_BLUE, DynamicColor.E_BLINKING_TYPE.BLINKING_SMOOTH, 1.8f);
				isInNeighborhoodMode = true;
			}
        }
		else
        {
			if (isInNeighborhoodMode)
			{
				dynamicColorVelocityArrow = null;
				dynamicColorVelocityText = null;
				VelocityArrowDrawer theVelocityArrow = UnityEngine.Object.FindObjectOfType(typeof(VelocityArrowDrawer)) as VelocityArrowDrawer;
				setVelocityArrowColor(theVelocityArrow.velocity_X, defaultArrowColor, defaultArrowColor);
				setVelocityArrowColor(theVelocityArrow.velocity_Y, defaultArrowColor, defaultArrowColor);
				isInNeighborhoodMode = false;
			}
        }

		return isInNeighborhoodMode;
	}

	public static void calculateClosestApproach()
    {
		Orbit playerOrbit = null;
		Orbit targetOrbit = null;

		// Is navigation active?
		if(Map.navigation.target == null)
        {
			resetApproachPhase();
			return;
        }

		// Does rocket player exist?
		if (PlayerController.main.player.Value == null || PlayerController.main.player.Value.mapPlayer == null)
		{
			resetApproachPhase();
			return;
		}

		// Get player orbit
		Trajectory playerTrajectory = PlayerController.main.player.Value.mapPlayer.Trajectory;

		if (playerTrajectory.paths.Count > 0)
        {
			if (playerTrajectory.paths[0] is Orbit orbit1)
			{
				playerOrbit = orbit1;
			}
		}

		if(playerOrbit == null)
        {
			resetApproachPhase();
			return;
		}

		// Get target orbit
		Trajectory targetTrajectory = Map.navigation.target.Trajectory;

		if (targetTrajectory.paths.Count > 0)
		{
			if (targetTrajectory.paths[0] is Orbit orbit2)
			{
				targetOrbit = orbit2;
			}
		}

		if (targetOrbit == null)
		{
			resetApproachPhase();
			return;
		}

		// Calculate the closest approach
		// ------------------------------
		ClosestApproachCalculator.T_ApproachData approachData = ClosestApproachCalculator.CalculateClosestApproachOnShortPeriod(playerOrbit, targetOrbit, C_SEARCH_CLOSEST_APPROACH_PERIOD);

		if(approachData.validity)
        {
			double deltaTime = approachData.date - WorldTime.main.worldTime;
			if (((deltaTime > 0.0) && (deltaTime < C_SEARCH_CLOSEST_APPROACH_PERIOD)) && (approachData.dist < C_NEIGHBORHOOD_DISTANCE))
			{
				Double2 distVector = approachData.locTarget.position - approachData.locPlayer.position;
				Double2 currentRelSpeedVector = targetOrbit.GetLocation(WorldTime.main.worldTime).velocity - playerOrbit.GetLocation(WorldTime.main.worldTime).velocity;

				setApproachPhase(distVector, currentRelSpeedVector, deltaTime);
			}
            else
            {
				resetApproachPhase();
			}
		}
		else
        {
			resetApproachPhase();
		}
	}

	private static float GetVelocityArrowLength(double velocity)
    {
		if(velocity < 0.1)
        {
			return 0.0f;
        }
		else if(velocity > 100.0f)
        {
			return 1.0f;
        }
        else
        {
			return (float)(0.1 + 0.9 * Math.Pow(velocity / 100.0, 2.0));
        }
    }

	private static float GetDistanceArrowLength(double distance)
	{
		const float MIN_DIST = 20.0f;
		const float MIN_LENGTH = 0.2f;
		const float MAX_DIST = 100.0f;
		const float MAX_LENGTH = 1.0f;

		if(distance < MIN_DIST)
        {
			return MIN_LENGTH;
        }
		else if (distance > MAX_DIST)
		{
			return MAX_LENGTH;
		}
		else
		{
			return (MIN_LENGTH + ((float)distance - MIN_DIST) * (MAX_LENGTH - MIN_LENGTH) / (MAX_DIST - MIN_DIST));
		}
	}

	private static float GetArrowLengthFactor()
    {
		double maxRadius = 0.9 * GameCamerasManager.main.world_Camera.camera.pixelHeight / 2.0;

		return (float)(0.4 * maxRadius);
	}

	private static Vector2 getHitPos(Double2 val, double length)
    {
		Double2 center;

		center.x = GameCamerasManager.main.world_Camera.camera.pixelWidth / 2.0;
		center.y = GameCamerasManager.main.world_Camera.camera.pixelHeight / 2.0;

		double maxRadius = 0.9 * GameCamerasManager.main.world_Camera.camera.pixelHeight / 2.0;

		double magnitude = val.magnitude;

		const double radiusOrigin = 0.4;

		double distFromOrigin = radiusOrigin * maxRadius + length / 2.0;

		Vector2 hitPos;
		hitPos.x = (float)(center.x + distFromOrigin * val.x / magnitude);
		hitPos.y = (float)(center.y + distFromOrigin * val.y / magnitude);

		return hitPos;
	}

	private static void displayDeltaVarrow(Location location)
	{
		VelocityArrowDrawer theVelocityArrow = UnityEngine.Object.FindObjectOfType(typeof(VelocityArrowDrawer)) as VelocityArrowDrawer;
		VelocityArrowDrawer.Arrow velocityArrow = theVelocityArrow.velocity_X;
		VelocityArrowDrawer.Arrow distanceArrow = theVelocityArrow.velocity_Y;

		
		if (!(PlayerController.main.player.Value is Rocket) || (bool)Map.manager.mapMode)
		{
			velocityArrow.SetActive(active: false);
			distanceArrow.SetActive(active: false);
			return;
		}

		float sizeRadius = PlayerController.main.player.Value.GetSizeRadius();
		if ((float)WorldView.main.viewDistance > sizeRadius * 50f + 50f)
		{
			velocityArrow.SetActive(active: false);
			distanceArrow.SetActive(active: false);
			return;
		}

		Vector2 origin = GameCamerasManager.main.world_Camera.camera.WorldToScreenPoint(WorldView.ToLocalPosition(location.position));

		float speed = (float)relativeSpeed.magnitude;
		float arrowLength = GetVelocityArrowLength(speed) * GetArrowLengthFactor();

		if (arrowLength > 0f)
		{
			Double2 directionNormal = relativeSpeed.Rotate(0f - GameCamerasManager.main.world_Camera.CameraRotationRadians) / speed;

			Vector2 arrowPos = getHitPos(relativeSpeed, arrowLength);

			setVelocityArrowColor(velocityArrow, dynamicColorVelocityArrow.getColor(), dynamicColorVelocityText.getColor());

			velocityArrow.Position(getVelocityString, arrowLength, arrowPos, directionNormal);
		}
		else
		{
			velocityArrow.SetActive(active: false);
		}

		// Distance
		float dist = (float)closestDistance.magnitude;
		float distanceArrowLength = GetDistanceArrowLength(dist) * GetArrowLengthFactor() / 2.0f;

		if (dist > 1.0f)
        {
			Double2 directionNormal = closestDistance.Rotate(0f - GameCamerasManager.main.world_Camera.CameraRotationRadians) / dist;

			Vector2 arrowPos = getHitPos(closestDistance, distanceArrowLength);

			setVelocityArrowColor(distanceArrow, closestDistanceColorArrow, closestDistanceColorText);

			distanceArrow.Position(getClosestDistanceString, distanceArrowLength, arrowPos, directionNormal);
		}
		else if(speed > 0.0)
        {
			// Impact trajectory - As direction, we take the opposite of velocity direction
			Double2 directionNormal = - relativeSpeed.Rotate(0f - GameCamerasManager.main.world_Camera.CameraRotationRadians) / speed;

			// Corresponds to the minimal arrow length
			float impactArrowLength = 0.2f * GetArrowLengthFactor() / 2.0f;

			Vector2 arrowPos = getHitPos(directionNormal, impactArrowLength);

			if(speed > 5.0)
            {
				setVelocityArrowColor(distanceArrow, dynamicColorImpactArrow.getColor(), dynamicColorImpactText.getColor());
			}
            else
            {
				setVelocityArrowColor(distanceArrow, closestDistanceColorArrow, closestDistanceColorText);
			}

			distanceArrow.Position(getImpactString, impactArrowLength, arrowPos, directionNormal);
		}
        else
        {
			distanceArrow.SetActive(false);
        }
	}


	private static void displayDeltaVarrow_NeighborhoodMode(Location location)
	{
		VelocityArrowDrawer theVelocityArrow = UnityEngine.Object.FindObjectOfType(typeof(VelocityArrowDrawer)) as VelocityArrowDrawer;
		VelocityArrowDrawer.Arrow velocityArrow = theVelocityArrow.velocity_X;
		VelocityArrowDrawer.Arrow distanceArrow = theVelocityArrow.velocity_Y;

		if (!(PlayerController.main.player.Value is Rocket) || (bool)Map.manager.mapMode)
		{
			velocityArrow.SetActive(active: false);
			distanceArrow.SetActive(active: false);
			return;
		}

		float sizeRadius = PlayerController.main.player.Value.GetSizeRadius();
		if ((float)WorldView.main.viewDistance > sizeRadius * 50f + 50f)
		{
			velocityArrow.SetActive(active: false);
			distanceArrow.SetActive(active: false);
			return;
		}

		Vector2 origin = GameCamerasManager.main.world_Camera.camera.WorldToScreenPoint(WorldView.ToLocalPosition(location.position));

		float speed = (float)relativeSpeed.magnitude;
		float arrowLength = GetVelocityArrowLength(speed) * GetArrowLengthFactor();

		if (arrowLength > 0f)
		{
			Double2 directionNormal = relativeSpeed.Rotate(0f - GameCamerasManager.main.world_Camera.CameraRotationRadians) / speed;

			Vector2 arrowPos = getHitPos(relativeSpeed, arrowLength);

			setVelocityArrowColor(velocityArrow, dynamicColorVelocityArrow.getColor(), dynamicColorVelocityText.getColor());

			velocityArrow.Position(getVelocityString, arrowLength, arrowPos, directionNormal);
		}
		else
		{
			velocityArrow.SetActive(active: false);
		}

		// Distance
		float dist = (float)distance.magnitude;
		float distanceArrowLength = GetDistanceArrowLength(dist) * GetArrowLengthFactor() / 2.0f;

		if (dist > 10.0f)
		{
			Double2 directionNormal = distance.Rotate(0f - GameCamerasManager.main.world_Camera.CameraRotationRadians) / dist;

			Vector2 arrowPos = getHitPos(distance, distanceArrowLength);

			setVelocityArrowColor(distanceArrow, distanceColorArrow, distanceColorText);

			distanceArrow.Position(getDistanceString, distanceArrowLength, arrowPos, directionNormal);
		}
		else
		{
			distanceArrow.SetActive(false);
		}
	}


	[HarmonyPrefix]
	public static bool OnLocationChange_Prefix(Location _, Location location)
	{
		if (!Map.manager.mapMode.Value) // view is in ship mode
		{
			calculateClosestApproach();

			if (isInApproachPhase)
			{
				try
				{
					displayDeltaVarrow(location);
					return false;
				}
				catch (Exception ex)
				{
					//FileLog.Log("Exception : " + ex.Message);
				}
			}
			else if(isInNeighborhood(location))
            {
				try
				{
					displayDeltaVarrow_NeighborhoodMode(location);
					return false;
				}
				catch (Exception ex)
				{
					//FileLog.Log("Exception : " + ex.Message);
				}
			}
		}
		
		// call the original method
		return true;
	}
}
