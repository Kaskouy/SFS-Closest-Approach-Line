using HarmonyLib;
using SFS.World;
using SFS.WorldBase;
using System;


public class Orbit_Utils
{
	public static double GetSpecificEnergy(Orbit orbit)
    {
		double specificEnergy = orbit.Planet.mass * (orbit.ecc * orbit.ecc - 1.0) / (2.0 * orbit.slr);
		return specificEnergy;
	}

	public static double GetAngularMomentum(Orbit orbit)
    {
		double angularMomentum = Math.Sqrt(orbit.Planet.mass * orbit.slr) * orbit.direction;
		return angularMomentum;
	}
}


[HarmonyPatch(typeof(Orbit), MethodType.Constructor, new Type[] { typeof(Location), typeof(bool), typeof(bool) })]
public class Orbit_OrbitPatch
{
	[HarmonyPrefix]
	public static bool Orbit_prefix(Location location)
    {
		return false;
    }

	[HarmonyPostfix]
	public static void Orbit_postfix(Orbit __instance, Location location, bool calculateTimeParameters, bool calculateEncounters)
    {
		__instance.orbitStartTime = location.time;

		Double3 angularMomentum_vector = Double3.Cross(location.position, location.velocity);
		Double2 eccentricity_vector = (Double2)(Double3.Cross((Double3)location.velocity, angularMomentum_vector) / location.planet.mass) - location.position.normalized;

		double specificEnergy = Math.Pow(location.velocity.magnitude, 2.0) / 2.0 - location.planet.mass / location.Radius;
		double angularMomentum = angularMomentum_vector.z;

		__instance.ecc = eccentricity_vector.magnitude;
		__instance.sma = location.planet.mass / (0.0 - 2.0 * specificEnergy);

		__instance.slr = angularMomentum * angularMomentum / location.planet.mass;
		__instance.periapsis = __instance.slr / (1.0 + __instance.ecc);
		
		__instance.arg = eccentricity_vector.AngleRadians;
		__instance.arg_Matrix = Matrix2x2_Double.Angle(__instance.arg);

		__instance.direction = Math.Sign(angularMomentum);

		if (specificEnergy < 0.0)
		{
			// General formula that also works for rectilinear orbits
			__instance.apoapsis = -location.planet.mass * (1.0 + __instance.ecc) / (2.0 * specificEnergy);
		}
		else
		{
			__instance.apoapsis = double.PositiveInfinity;
		}

		__instance.semiMinorAxis = Kepler.GetSemiMinorAxis(__instance.sma, __instance.ecc);


		bool isEscaping = __instance.apoapsis >= location.planet.SOI;

		double trueAnomaly_Out = Kepler.NormalizeAngle(location.position.AngleRadians - __instance.arg);
		Traverse.Create(__instance).Field("trueAnomaly_Out").SetValue(trueAnomaly_Out);
		Traverse.Create(__instance).Field("location_Out").SetValue(location);

		if (calculateTimeParameters)
		{
			__instance.period = (isEscaping ? 0.0 : Kepler.GetPeriod(__instance.sma, location.planet.mass));
			__instance.meanMotion = Kepler.GetMeanMotion(__instance.sma, location.planet.mass);

			double timeFromPeri = 0.0;
			KeplerSolver.GetTimeAtPosition(location.planet.mass, __instance.periapsis, specificEnergy, location.Radius, trueAnomaly_Out, ref timeFromPeri);
			__instance.periapsisPassageTime = location.time - timeFromPeri * (double)(__instance.direction) - 10.0 * __instance.period;
		}

		if (isEscaping)
		{
			double loc_orbitEndTime;

			if (location.planet.SOI == double.PositiveInfinity)
			{
				loc_orbitEndTime = double.PositiveInfinity;
			}
			else
			{
				double loc_trueAnomaly = Math.Acos((__instance.slr / location.planet.SOI - 1.0) / __instance.ecc);
				double timeFromPeri = 0.0;

				KeplerSolver.GetTimeAtPosition(location.planet.mass, __instance.periapsis, specificEnergy, location.planet.SOI, loc_trueAnomaly, ref timeFromPeri);

				loc_orbitEndTime = __instance.periapsisPassageTime + timeFromPeri;
			}

			Planet parentPlanet = location.planet.parentBody;
			//Traverse.Create<Orbit>().Method("SetOrbitType", PathType.Escape, parentPlanet, loc_orbitEndTime);

			__instance.pathType = PathType.Escape;
			__instance.orbitEndTime = loc_orbitEndTime;
			Traverse.Create(__instance).Field("nextPlanet").SetValue(parentPlanet);
		}
		else
		{
			//Planet parentPlanet = null;
			//Traverse.Create<Orbit>().Method("SetOrbitType", PathType.Eternal, parentPlanet, double.PositiveInfinity);

			__instance.pathType = PathType.Eternal;
			__instance.orbitEndTime = double.PositiveInfinity;
			Traverse.Create(__instance).Field("nextPlanet").SetValue(null);
		}
		if (calculateEncounters)
		{
			//Traverse.Create<Orbit>().Method("FindEncounters", location.time, isEscaping ? double.PositiveInfinity : (location.time + __instance.period * 0.99));
			//FindEncounters(location.time, flag ? double.PositiveInfinity : (location.time + period * 0.99));
			double startWindow = location.time;
			double endWindow = isEscaping ? double.PositiveInfinity : (location.time + __instance.period * 0.99);
			Traverse.Create(__instance).Method("FindEncounters", startWindow, endWindow).GetValue(startWindow, endWindow);
		}
    }
}


[HarmonyPatch(typeof(Orbit), "GetVelocityAtTrueAnomaly")]
public class Orbit_GetVelocityAtTrueAnomaly_Patch
{
	[HarmonyPrefix]
	public static bool GetVelocityAtTrueAnomaly_Prefix(double trueAnomaly)
	{
		// skip the original method
		return false;
	}

	[HarmonyPostfix]
	public static /*Double2*/ void GetVelocityAtTrueAnomaly_Postfix(ref Double2 __result, Orbit __instance, double trueAnomaly)
    {
		double angularMomentum = Orbit_Utils.GetAngularMomentum(__instance);
		double specificEnergy = Orbit_Utils.GetSpecificEnergy(__instance);

		double normalizedTrueAnomaly = Kepler.NormalizeAngle(trueAnomaly);
		double radiusAtTrueAnomaly = Kepler.GetRadiusAtTrueAnomaly(__instance.slr, __instance.ecc, normalizedTrueAnomaly);

		
		double V_theta = angularMomentum / radiusAtTrueAnomaly;
		double V2 = 2.0 * (specificEnergy + __instance.Planet.mass / radiusAtTrueAnomaly);
		double V_r2 = Math.Max(V2 - V_theta * V_theta, 0.0);
		double V_r = Math.Sqrt(V_r2);

		int sign = 1;
		if(normalizedTrueAnomaly < 0.0) { sign = -1; }
		//if (Math.Sign(normalizedTrueAnomaly) != __instance.direction) { V_r = -V_r; } // For some obscure reasons, calling Math.Sign(normalizedTrueAnomaly) sometimes causes problems (orbits blinking)
		if (sign != __instance.direction) { V_r = -V_r; }

		Double2 velocity = new Double2(V_r, V_theta);
		velocity = velocity.Rotate(normalizedTrueAnomaly + __instance.arg);
		//return velocity;
		__result = velocity;
	}
}

[HarmonyPatch(typeof(Orbit), "GetLastTrueAnomalyPassTime")]
public class Orbit_GetLastTrueAnomalyPassTime_Patch
{
	[HarmonyPrefix]
	public static bool GetLastTrueAnomalyPassTime_Prefix(double time, double trueAnomaly)
	{
		// skip the original method
		return false;
	}

	[HarmonyPostfix]
	public static double GetLastTrueAnomalyPassTime_Postfix(double __result, Orbit __instance, double time, double trueAnomaly, Location ___location_Out)
    {
		double specificEnergy = Orbit_Utils.GetSpecificEnergy(__instance);
		double timeFromPeri = 0.0;
		double radius = __instance.slr / (1.0 + __instance.ecc * Math.Cos(trueAnomaly));
		KeplerSolver.GetTimeAtPosition(___location_Out.planet.mass, __instance.periapsis, specificEnergy, radius, trueAnomaly, ref timeFromPeri);

		double num = __instance.periapsisPassageTime + timeFromPeri * (double)(__instance.direction);

		if (__instance.pathType == PathType.Escape)
		{
			return num;
		}

		return num + (double)Math_Utility.GetFitsTime(__instance.period, num, time) * __instance.period;
	}
}


[HarmonyPatch(typeof(Orbit), "UpdateLocation")]
public class Orbit_Patch
{
    [HarmonyPrefix]
    public static bool UpdateLocation_Prefix(double newTime)
    {
        // skip the original method
        return false;
    }

    [HarmonyPostfix]
    public static void UpdateLocation_Postfix(Orbit __instance, double newTime, ref Location ___location_Out, ref double ___trueAnomaly_Out)
    {

		if (___location_Out.time != newTime && !double.IsNaN(newTime))
		{
			double radius = 0.0;
			double trueAnomaly = 0.0;
			double specificEnergy = Orbit_Utils.GetSpecificEnergy(__instance);
			KeplerSolver.GetPositionAtTime(___location_Out.planet.mass, __instance.periapsis, specificEnergy, (newTime - __instance.periapsisPassageTime) * (double)(__instance.direction), ref radius, ref trueAnomaly);
			double argument = trueAnomaly + __instance.arg;

			// Calculate position
			Double2 position = new Double2(radius * Math.Cos(argument), radius * Math.Sin(argument));

			// Calculate velocity
			Double2 velocity = __instance.GetVelocityAtTrueAnomaly(trueAnomaly);

			Location location = new Location(newTime, ___location_Out.planet, position, velocity);
			if (!double.IsNaN(trueAnomaly) && !double.IsNaN(location.position.x) && !double.IsNaN(location.position.y) && !double.IsNaN(location.velocity.x) && !double.IsNaN(location.velocity.y))
			{
				___location_Out = location;
				___trueAnomaly_Out = trueAnomaly;
			}
		}
	}
}
