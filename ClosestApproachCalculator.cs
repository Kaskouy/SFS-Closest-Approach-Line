using SFS.World;
using SFS.Navigation;
using HarmonyLib;
using System;

public class ClosestApproachCalculator
{
	private static Player thePlayer;

	// This structure is used to encapsulate all the relevant data relative to a specific approach situation
	public struct T_ApproachData
	{
		public double date;      // the date at which the approach was evaluated
		public Location locPlayer;   // the position of the player object on its orbit
		public Location locTarget;   // the position of the target object on its orbit
		public double dist;      // distance between the 2 objects
		public double relSpeed;  // the relative speed between the 2 objects (if positive, they are getting further from eachother)
		public bool validity;    // a flag that indicates if data is valid or not
	}

	// data to memorize the latest approach data and how much time it's valid
	private static bool lastApproachValid = false;
	private static double lastApproachCalculationDate = -1.0;
	private static T_ApproachData lastApproachData = new T_ApproachData();

	// data to memorize the latest approach data and how much time it's valid, for the multi-turn approach
	private static double lastApproacMultiTurnCalculationDate = -1.0;
	private static bool lastApproachMultiTurnValid1 = false;
	private static T_ApproachData lastApproachData_MultiTurn1 = new T_ApproachData();
	private static uint lastApproachData_nbTurns1 = 0;
	private static bool lastApproachMultiTurnValid2 = false;
	private static T_ApproachData lastApproachData_MultiTurn2 = new T_ApproachData();
	private static uint lastApproachData_nbTurns2 = 0;

	// Once the closest approach data has been calculated, it's considered valid for that many seconds before we recalculate it (unless something changes...)
	private const double C_APPROACH_DATA_TIME_VALIDITY = 2.0;

	public static void setPlayer(Player player)
    {
		thePlayer = player;
    }

	public static void resetLastApproachDataValidity()
	{
		lastApproachValid = false;
		lastApproachCalculationDate = -1.0;
		lastApproachData = new T_ApproachData();
		lastApproachData.validity = false;
	}

	private static void memorizeLastApproachData(T_ApproachData approachData)
	{
		lastApproachValid = true;
		lastApproachCalculationDate = WorldTime.main.worldTime;
		lastApproachData = approachData;
	}

	private static bool approachDataNeedsRecalculation()
	{
		if (!lastApproachValid)
		{
			// last approach is not valid (player has just loaded the game, he just switched target...) --> needs to be calculated
			return true;
		}

		if ((thePlayer != null) && (thePlayer is Rocket rocket))
		{
			bool enginesOn = (rocket.throttle.output_Throttle.Value > 0.0);
			bool rcsOn = (rocket.arrowkeys.rcs.Value == true);
			bool timeOut = (WorldTime.main.worldTime > lastApproachCalculationDate + C_APPROACH_DATA_TIME_VALIDITY);

			if (enginesOn || rcsOn || timeOut)
			{
				// engines/RCS are on, the player is changing his trajectory --> instantly recompute approach data to let him follow accurately the situation
				// ...Or the latest data is too old.
				return true;
			}
			else
			{
				// the player is not modifying his trajectory, and the latest information is still recent enough --> No recalculation
				return false;
			}
		}
		else
		{
			// kinda unexpected, return true by default (--> recalculate closest approach)
			return true;
		}
	}


	public static void resetLastApproachDataValidity_MultiTurn()
	{
		lastApproacMultiTurnCalculationDate = -1.0;
		lastApproachMultiTurnValid1 = false;
		lastApproachData_MultiTurn1 = new T_ApproachData();
		lastApproachData_MultiTurn1.validity = false;
		lastApproachData_nbTurns1 = 0;
		lastApproachMultiTurnValid2 = false;
		lastApproachData_MultiTurn2 = new T_ApproachData();
		lastApproachData_MultiTurn2.validity = false;
		lastApproachData_nbTurns2 = 0;
	}

	private static void memorizeLastApproachData_MultiTurn(T_ApproachData approachData1, uint nbTurns1, T_ApproachData approachData2, uint nbTurns2)
	{
		lastApproacMultiTurnCalculationDate = WorldTime.main.worldTime;
		lastApproachMultiTurnValid1 = true;
		lastApproachData_MultiTurn1 = approachData1;
		lastApproachData_nbTurns1 = nbTurns1;
		lastApproachMultiTurnValid2 = true;
		lastApproachData_MultiTurn2 = approachData2;
		lastApproachData_nbTurns2 = nbTurns2;
	}

	private static bool approachDataNeedsRecalculation_MultiTurn()
	{
		if (!lastApproachMultiTurnValid1 || !lastApproachMultiTurnValid2)
		{
			// last approach is not valid (player has just loaded the game, he just switched target...) --> needs to be calculated
			return true;
		}

		if ((thePlayer != null) && (thePlayer is Rocket rocket))
		{
			bool enginesOn = (rocket.throttle.output_Throttle.Value > 0.0);
			bool rcsOn = (rocket.arrowkeys.rcs.Value == true);
			bool timeOut = (WorldTime.main.worldTime > lastApproacMultiTurnCalculationDate + C_APPROACH_DATA_TIME_VALIDITY);

			if (enginesOn || rcsOn || timeOut)
			{
				// engines/RCS are on, the player is changing his trajectory --> instantly recompute approach data to let him follow accurately the situation
				// ...Or the latest data is too old.
				return true;
			}
			else
			{
				// the player is not modifying his trajectory, and the latest information is still recent enough --> No recalculation
				return false;
			}
		}
		else
		{
			// kinda unexpected, return true by default (--> recalculate closest approach)
			return true;
		}
	}



	private static T_ApproachData GetApproachAtDate(Orbit orbit_A, Orbit orbit_B, double time)
	{
		const double C_MIN_DISTANCE = 0.1; // Below this threshold, we consider we have a perfect encounter

		T_ApproachData approachData = new T_ApproachData();

		approachData.date = time;

		// Get locations of objects on each orbit
		approachData.locPlayer = orbit_A.GetLocation(time);
		approachData.locTarget = orbit_B.GetLocation(time);

		// Get distance
		Double2 relativePos = approachData.locTarget.position - approachData.locPlayer.position;
		approachData.dist = relativePos.magnitude;

		// Get relative speed
		if (approachData.dist < C_MIN_DISTANCE)
		{
			approachData.relSpeed = 0.0;
		}
		else
		{
			Double2 relativeVelocity = approachData.locTarget.velocity - approachData.locPlayer.velocity;
			approachData.relSpeed = Double2.Dot(relativePos, relativeVelocity) / approachData.dist;
		}

		approachData.validity = true;

		return approachData;
	}

	private static T_ApproachData GetApproachAtArgument(Orbit orbit_A, Orbit orbit_B, double fromTime, double argument)
	{
		const double C_MIN_DISTANCE = 0.1; // Below this threshold, we consider we have a perfect encounter

		T_ApproachData approachData = new T_ApproachData();

		// Calculate time at which object A reaches desired angle
		double passageTime = orbit_A.GetNextAnglePassTime(fromTime, argument);

		approachData.date = passageTime;

		// Calculate location A
		Double2 position = orbit_A.GetPositionAtAngle(argument);
		Double2 velocity = orbit_A.GetVelocityAtAngle(argument);
		approachData.locPlayer = new Location(passageTime, orbit_A.Planet, position, velocity);

		// Calculate location B
		approachData.locTarget = orbit_B.GetLocation(passageTime);

		// Get distance
		Double2 relativePos = approachData.locTarget.position - approachData.locPlayer.position;
		approachData.dist = relativePos.magnitude;

		// Get relative speed
		if (approachData.dist < C_MIN_DISTANCE)
		{
			approachData.relSpeed = 0.0;
		}
		else
		{
			Double2 relativeVelocity = approachData.locTarget.velocity - approachData.locPlayer.velocity;
			approachData.relSpeed = Double2.Dot(relativePos, relativeVelocity) / approachData.dist;
		}

		approachData.validity = true;

		return approachData;
	}

	private static T_ApproachData getApproachData_DichotomicMethod(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
	{
		double time = (approachData1.date + approachData2.date) / 2.0;

		return GetApproachAtDate(orbit_A, orbit_B, time);
	}

	private static T_ApproachData getApproachData_QuadraticApproximation(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
	{
		double deltaT = approachData2.date - approachData1.date;
		double time = approachData1.date - approachData1.relSpeed * deltaT / (approachData2.relSpeed - approachData1.relSpeed);

		return GetApproachAtDate(orbit_A, orbit_B, time);
	}

	private static T_ApproachData getApproachData_CubicApproximation(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
    {
		double deltaX = approachData2.date - approachData1.date;
		double p = (approachData2.dist - approachData1.dist) / deltaX;

		double a = 3 * (approachData2.relSpeed + approachData1.relSpeed - 2.0 * p) / (deltaX * deltaX);
		double b = 2 * (3.0 * p - approachData2.relSpeed - 2.0 * approachData1.relSpeed) / deltaX;
		double c = approachData1.relSpeed;

		double delta = b * b - 4.0 * a * c;

		if(delta < 0.0)
        {
			//FileLog.Log("getApproachData_CubicApproximation : ERROR : delta < 0.0");
			return getApproachData_DichotomicMethod(orbit_A, orbit_B, approachData1, approachData2); // better than nothing...
		}
        else
        {
			double new_time;

			new_time = approachData1.date + (-b + Math.Sqrt(delta)) / (2.0 * a);

			if((new_time < approachData1.date) || (new_time > approachData2.date))
            {
				//FileLog.Log("getApproachData_CubicApproximation : ERROR : new time out of range");
				//FileLog.Log("getApproachData_CubicApproximation : A = " + a + "; B = " + b + "; C = " + c);
				//FileLog.Log("getApproachData_CubicApproximation : delta = " + delta);
				//FileLog.Log("getApproachData_CubicApproximation : time1 = " + approachData1.date + "; time2 = " + approachData2.date + "; new time = " + new_time);
				return getApproachData_DichotomicMethod(orbit_A, orbit_B, approachData1, approachData2); // better than nothing...
			}
            else
            {
				T_ApproachData newApproachData = GetApproachAtDate(orbit_A, orbit_B, new_time);

				double q = 2.0 * a * (new_time - approachData1.date) + b;

				//FileLog.Log("getApproachData_CubicApproximation : q  = " + q);
				//FileLog.Log("getApproachData_CubicApproximation : new_time  = " + new_time + "; rel_speed  = " + newApproachData.relSpeed);

				return GetApproachAtDate(orbit_A, orbit_B, new_time);
			}
        }
	}

	private static T_ApproachData getApproachData_SmartCutStrategy(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
    {
		double newTime;

		if (Math.Abs(approachData1.relSpeed) < Math.Abs(approachData2.relSpeed))
        {
			newTime = approachData1.date + (approachData2.date - approachData1.date) / 8.0;
		}
		else
        {
			newTime = approachData2.date - (approachData2.date - approachData1.date) / 8.0;
		}

		return GetApproachAtDate(orbit_A, orbit_B, newTime);
	}

	private static T_ApproachData getApproachData_LinerarMovementApproximation(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
    {
		// Consists in approximating the movement as a linear one. It's equivalent to neglecting gravity.
		// It's ok if the objects are close to eachother, it can work great in those cases. Otherwise, it can be very bad!
		
		// Position of target with respect to the player at time 1 and time 2
		Double2 P1 = approachData1.locTarget.position - approachData1.locPlayer.position;
		Double2 P2 = approachData2.locTarget.position - approachData2.locPlayer.position;

		Double2 P1P2 = P2 - P1;

		double Tau = -Double2.Dot(P1, P1P2) / Math.Pow(P1P2.magnitude, 2.0);

		double new_time = approachData1.date + Tau * (approachData2.date - approachData1.date);

		T_ApproachData newApproachData = GetApproachAtDate(orbit_A, orbit_B, new_time);
		
		//FileLog.Log("getApproachData_LinerarMovementApproximation : Tau = " + Tau);
		//FileLog.Log("getApproachData_LinerarMovementApproximation : Time1 = " + approachData1.date + "; Time2 = " + approachData2.date + "; NewTime = " + newApproachData.date);
		//FileLog.Log("getApproachData_LinerarMovementApproximation : Dist1 = " + approachData1.dist + "; Dist2 = " + approachData2.dist + "; NewDist = " + newApproachData.dist);
		//FileLog.Log("getApproachData_LinerarMovementApproximation : Vel1  = " + approachData1.relSpeed + "; Vel2  = " + approachData2.relSpeed + "; NewVel  = " + newApproachData.relSpeed);

		return newApproachData;
	}

	private static bool isLocalMinimum(T_ApproachData approachData)
    {
		const double MIN_ABSOLUTE_SPEED = 0.1;    // approach speed must be below 0.1 m/s or...
		const double MIN_RELATIVE_SPEED = 0.0001; // approach speed must be below this value times the relative speed between target and player

		double relativeVelocity = (approachData.locPlayer.velocity - approachData.locTarget.velocity).magnitude;

		if ( (Math.Abs(approachData.relSpeed) < MIN_ABSOLUTE_SPEED) || 
			 (Math.Abs(approachData.relSpeed) < MIN_RELATIVE_SPEED * relativeVelocity) )
		{
			return true;
		}
		else
        {
			return false;
        }
	}


	private static T_ApproachData calculateMinimalApproachBetweenTwoDates(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
	{
		T_ApproachData approachData;

		// CHECK DATES
		// -----------
		if (approachData1.date > approachData2.date)
		{
			// We expect date 1 to be lower than date 2 --> return invalid value
			approachData = new T_ApproachData();
			approachData.validity = false;
			return approachData;
		}

		// CHECK OBVIOUS CASES
		// -------------------
		if (approachData1.relSpeed > 0.0)
		{
			if (approachData2.relSpeed > 0.0)
			{
				// Both objects only get further from eachother from date 1 --> date 1 is date of closest approach in this interval
				return approachData1;
			}
			else
			{
				// By the end, both objects get closer --> return minimal approach between approach at date 1 and date 2
				if (approachData1.dist < approachData2.dist)
				{
					return approachData1;
				}
				else
				{
					return approachData2;
				}
			}
		}

		if (approachData2.relSpeed < 0.0)
		{
			// We already know that: approachData1.relSpeed < 0.0
			// The objects keep getting closer on the whole period --> closest approach is at date 2
			return approachData2;
		}

		// ACCURATE MINIMUM CALCULATION
		// ----------------------------
		// Now we are sure that:
		// - T2 > T1
		// - approachData1.relSpeed < 0.0: the objects are approaching at date 1
		// - approachData2.relSpeed > 0.0: the objects are getting further at date 2
		// => There is a minimal approach between those 2 dates

		const uint MAX_ITERATIONS = 50;
		uint nbIterations = 0;

		T_ApproachData loc_ApproachData1 = approachData1;
		T_ApproachData loc_ApproachData2 = approachData2;

		while (nbIterations < MAX_ITERATIONS)
		{
			nbIterations++;

			// Calculate min/max speed
			double min_relSpeed = Math.Abs(loc_ApproachData1.relSpeed);
			double max_relSpeed = Math.Abs(loc_ApproachData2.relSpeed);

			if (min_relSpeed > max_relSpeed)
			{
				double temp_relSpeed = min_relSpeed;
				min_relSpeed = max_relSpeed;
				max_relSpeed = temp_relSpeed;
			}

			// Calculate next approximation
			// ----------------------------
			if (max_relSpeed < 63.0 * min_relSpeed)
			{
				// relative speeds at bounds are relatively close -> use the more precise quadratic approximation
				//approachData = getApproachData_QuadraticApproximation(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2);
				approachData = getApproachData_CubicApproximation(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2);
				//FileLog.Log("   Cubic approx!");
				//FileLog.Log("calculateMinimalApproachBetweenTwoDates : Time1 = " + loc_ApproachData1.date + "; Time2 = " + loc_ApproachData2.date + "; NewTime = " + approachData.date);
				//FileLog.Log("calculateMinimalApproachBetweenTwoDates : Dist1 = " + loc_ApproachData1.dist + "; Dist2 = " + loc_ApproachData2.dist + "; NewDist = " + approachData.dist);
				//FileLog.Log("calculateMinimalApproachBetweenTwoDates : Vel1  = " + loc_ApproachData1.relSpeed + "; Vel2  = " + loc_ApproachData2.relSpeed + "; NewVel  = " + approachData.relSpeed);
				
			}
            else 
			{
				//FileLog.Log("   Smart cut!");
				approachData = getApproachData_SmartCutStrategy(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2);
			}

			// If answer found with a satisfying precision, bye bye!
			// -----------------------------------------------------
			if (isLocalMinimum(approachData))
			{
				//FileLog.Log(" Nb iterations = " + nbIterations);
				return approachData;
			}

			// Otherwise, prepare data for the next iteration
			// ----------------------------------------------
			if (approachData.relSpeed < 0.0)
			{
				loc_ApproachData1 = approachData; // for next iteration, calculated approach data replaces value 1
			}
			else
			{
				loc_ApproachData2 = approachData; // for next iteration, calculated approach data replaces value 2
			}
		}

		approachData = new T_ApproachData();
		approachData.validity = false;
		return approachData;
	}

	private static T_ApproachData[] GetApproachDataSamples(Orbit orbit_A, Orbit orbit_B, double time_start, double time_end, uint nbPoints)
	{
		T_ApproachData[] tab_ApproachData = new T_ApproachData[nbPoints];

		for (int i = 0; i < nbPoints; i++)
		{
			double time = time_start + i * (time_end - time_start) / (nbPoints - 1);
			tab_ApproachData[i] = GetApproachAtDate(orbit_A, orbit_B, time);
		}

		return tab_ApproachData;
	}

	private static T_ApproachData CalculateClosestApproachOnPeriod(Orbit orbit_A, Orbit orbit_B, double time_start, double time_end)
	{
		// First, sample a few points...
		const uint nbPoints = 20;
		T_ApproachData[] tab_ApproachData = GetApproachDataSamples(orbit_A, orbit_B, time_start, time_end, nbPoints);

		// Search the minimum approach between those points
		int i_min = 0;
		double min_dist = 0.0;

		for (int i = 0; i < tab_ApproachData.Length; i++)
		{
			if ((i == 0) || (tab_ApproachData[i].dist < min_dist))
			{
				min_dist = tab_ApproachData[i].dist;
				i_min = i;
			}
		}

		// handle obvious cases
		if ((i_min == 0) && (tab_ApproachData[i_min].relSpeed > 0.0))
		{
			// First point is closest, and objects are getting further after it --> This is the closest overall
			return tab_ApproachData[i_min];
		}

		if ((i_min == tab_ApproachData.Length - 1) && (tab_ApproachData[i_min].relSpeed < 0.0))
		{
			// Last point is closest, and objects are still approaching --> This is the closest overall
			return tab_ApproachData[i_min];
		}

		// General case: find the accurate minimum between a 2 points interval
		if (tab_ApproachData[i_min].relSpeed > 0.0)
		{
			return calculateMinimalApproachBetweenTwoDates(orbit_A, orbit_B, tab_ApproachData[i_min - 1], tab_ApproachData[i_min]);
		}
		else
		{
			return calculateMinimalApproachBetweenTwoDates(orbit_A, orbit_B, tab_ApproachData[i_min], tab_ApproachData[i_min + 1]);
		}
	}

	public static T_ApproachData CalculateClosestApproach(Orbit orbit_A, Orbit orbit_B)
	{
		if (!approachDataNeedsRecalculation())
		{
			return lastApproachData; // Skip calculations and return last result
		}

		// Calculate start time (can be now if it's about the current trajectory, or in the future if it's an anticipated trajectory)
		// --------------------
		double start_time = WorldTime.main.worldTime;

		if (start_time < orbit_A.orbitStartTime)
		{
			start_time = orbit_A.orbitStartTime;
		}

		// Calculate end time (if the ship has an encounter planned for example, so that we don't search a closest approach after the encounter)
		// ------------------
		double end_time = Double.PositiveInfinity;

		if (orbit_A.pathType != PathType.Eternal)
		{
			end_time = orbit_A.orbitEndTime;
		}

		// if target also has an end time, take it into account too
		if ((orbit_B.pathType != PathType.Eternal) && (orbit_B.orbitEndTime < end_time))
		{
			end_time = orbit_B.orbitEndTime;
		}

		// Would be weird but just in case...
		if (end_time < start_time)
		{
			resetLastApproachDataValidity();
			return lastApproachData;
		}


		bool is_A_Periodic = (orbit_A.ecc < 1.0) && (orbit_A.pathType != PathType.Escape);
		bool is_B_Periodic = (orbit_B.ecc < 1.0) && (orbit_B.pathType != PathType.Escape);

		if (is_A_Periodic && (!is_B_Periodic || orbit_A.period < 3.0 * orbit_B.period))
		{
			// Orbit A is periodic, and its period is at most three times B period --> research accurately closest approach
			// ------------------------------------------------------------------------------------------------------------
			double min_period;

			if (!is_B_Periodic)
			{
				min_period = orbit_A.period;
			}
			else
			{
				min_period = Math.Min(orbit_A.period, orbit_B.period);
			}

			double maxTime = Math.Min(start_time + orbit_A.period, end_time);
			double searchTime = maxTime - start_time;

			double startSearchPeriod = start_time;
			bool stop = false;
			T_ApproachData bestApproachData = new T_ApproachData();
			bestApproachData.validity = false;
			bestApproachData.dist = Double.PositiveInfinity;
			double endSearchPeriod;

			do
			{
				endSearchPeriod = startSearchPeriod + min_period;

				// If we reached max_time, that iteration will be the last
				if (endSearchPeriod > start_time + 0.999 * searchTime)
				{
					endSearchPeriod = maxTime;
					stop = true;
				}

				T_ApproachData approachData = CalculateClosestApproachOnPeriod(orbit_A, orbit_B, startSearchPeriod, endSearchPeriod);

				if (approachData.validity)
				{
					if (!bestApproachData.validity)
					{
						bestApproachData = approachData; // first valid data
					}
					else if (approachData.dist < bestApproachData.dist)
					{
						bestApproachData = approachData; // found better approach data
					}
				}

				// Preparing for the next iteration: we'll search on the following time interval
				startSearchPeriod = endSearchPeriod;

			} while (!stop);

			if (bestApproachData.validity)
			{
				memorizeLastApproachData(bestApproachData);
			}
			else
			{
				resetLastApproachDataValidity();
			}
			return bestApproachData;
		}
		else
		{
			// Period A is significantly higher than period B --> search approach at node
			// ----------------------------------------------
			double angleIntersection1, angleIntersection2;

			bool intersects = Intersection.GetIntersectionAngles(orbit_A, orbit_B, out angleIntersection1, out angleIntersection2);

			if (intersects)
			{
				// Orbits intersect eachother --> evaluate the situation at each of the crossing point
				// -----------------------------------------------------------------------------------
				T_ApproachData approachData1 = GetApproachAtArgument(orbit_A, orbit_B, start_time, angleIntersection1);
				T_ApproachData approachData2 = GetApproachAtArgument(orbit_A, orbit_B, start_time, angleIntersection2);

				bool data1Valid = approachData1.validity && (approachData1.date > start_time) && (approachData1.date < end_time);
				bool data2Valid = approachData2.validity && (approachData2.date > start_time) && (approachData2.date < end_time);

				if (data1Valid && data2Valid)
				{
					// Both approaches are valid, choose the best situation
					if (approachData1.dist < approachData2.dist)
					{
						memorizeLastApproachData(approachData1);
					}
					else
					{
						memorizeLastApproachData(approachData2);
					}
				}
				else if (data1Valid)
				{
					// Only approach at node 1 is valid
					memorizeLastApproachData(approachData1);
				}
				else if (data2Valid)
				{
					// Only approach at node 2 is valid
					memorizeLastApproachData(approachData2);
				}
				else
				{
					// None is valid
					resetLastApproachDataValidity();
				}

				return lastApproachData;
			}
			else
			{
				// No intersection -> Calculate approach at closest geometric point
				// ----------------------------------------------------------------
				T_ApproachData approachData1 = GetApproachAtArgument(orbit_A, orbit_B, start_time, angleIntersection1);

				if (approachData1.validity && (approachData1.date > start_time) && (approachData1.date < end_time))
				{
					memorizeLastApproachData(approachData1);
				}
				else
				{
					resetLastApproachDataValidity();
				}

				return lastApproachData;
			}
		}
	}


	public static void CalculateClosestApproach_MultiTurn(Orbit orbit_A, Orbit orbit_B, out T_ApproachData bestApproachData1, out uint nbTurns1, out T_ApproachData bestApproachData2, out uint nbTurns2)
    {
		if (!approachDataNeedsRecalculation_MultiTurn())
		{
			// Skip calculations and return last result
			nbTurns1 = lastApproachData_nbTurns1;
			bestApproachData1 = lastApproachData_MultiTurn1; 
			nbTurns2 = lastApproachData_nbTurns2;
			bestApproachData2 = lastApproachData_MultiTurn2;
            return;
		}

		// Check that both orbits have no encounter/escape event scheduled
		if ( (orbit_A.pathType != PathType.Eternal) || (orbit_B.pathType != PathType.Eternal))
        {
			// return invalid data
			nbTurns1 = 0;
			bestApproachData1 = lastApproachData_MultiTurn1;
			nbTurns2 = 0;
			bestApproachData2 = lastApproachData_MultiTurn2;
			return;
		}

		double angleIntersection1, angleIntersection2;

		bool intersects = Intersection.GetIntersectionAngles(orbit_A, orbit_B, out angleIntersection1, out angleIntersection2);

		// If the orbit don't intersect, don't calculate anything
		if(!intersects)
        {
			// return invalid data
			nbTurns1 = 0;
			bestApproachData1 = lastApproachData_MultiTurn1;
			nbTurns2 = 0;
			bestApproachData2 = lastApproachData_MultiTurn2;
			return;
		}

		// Calculate start time (can be now if it's about the current trajectory, or in the future if it's an anticipated trajectory)
		// --------------------
		double start_time = WorldTime.main.worldTime;

		if (start_time < orbit_A.orbitStartTime)
		{
			start_time = orbit_A.orbitStartTime;
		}

		const uint NB_MAX_TURNS = 20;

		double weightedBestApproach1 = 0, weightedBestApproach2 = 0;
		double curStartTime = start_time + orbit_A.period; // to start after one period

		// Initialization
		bestApproachData1 = new T_ApproachData();
		bestApproachData2 = new T_ApproachData();
		bestApproachData1.validity = false;
		bestApproachData2.validity = false;
		nbTurns1 = nbTurns2 = 0;

		// Calculate approach at each node on each turn and memorize the best
		// ------------------------------------------------------------------
		for (uint i_turn = 2; i_turn <= NB_MAX_TURNS; i_turn++)
        {
			T_ApproachData curApproachData1 = GetApproachAtArgument(orbit_A, orbit_B, curStartTime, angleIntersection1);
			T_ApproachData curApproachData2 = GetApproachAtArgument(orbit_A, orbit_B, curStartTime, angleIntersection2);

			if(i_turn == 2)
            {
				// first iteration
				nbTurns1 = i_turn;
				nbTurns2 = i_turn;
				weightedBestApproach1 = Math.Sqrt(i_turn) * curApproachData1.dist;
				weightedBestApproach2 = Math.Sqrt(i_turn) * curApproachData2.dist;
				bestApproachData1 = curApproachData1;
				bestApproachData2 = curApproachData2;
			}
			else
            {
				double cur_weightedBestApproach1 = Math.Sqrt(i_turn) * curApproachData1.dist;
				double cur_weightedBestApproach2 = Math.Sqrt(i_turn) * curApproachData2.dist;

				if(cur_weightedBestApproach1 < weightedBestApproach1)
                {
					weightedBestApproach1 = cur_weightedBestApproach1;
					nbTurns1 = i_turn;
					bestApproachData1 = curApproachData1;
				}

				if (cur_weightedBestApproach2 < weightedBestApproach2)
				{
					weightedBestApproach2 = cur_weightedBestApproach2;
					nbTurns2 = i_turn;
					bestApproachData2 = curApproachData2;
				}
			}

			// For next loop: search for the next revolution
			curStartTime += orbit_A.period;
		}

		memorizeLastApproachData_MultiTurn(bestApproachData1, nbTurns1, bestApproachData2, nbTurns2);
	}

}
