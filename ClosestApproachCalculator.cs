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
			if (max_relSpeed < 2.0 * min_relSpeed)
			{
				// relative speeds at bounds are relatively close -> use the more precise quadratic approximation
				approachData = getApproachData_QuadraticApproximation(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2);
			}
			else
			{
				// relative speeds are significantly different -> calculate the medium value, as the quadratic approximation wouldn't allow much progress
				approachData = getApproachData_DichotomicMethod(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2);
			}

			// If answer found with a satisfying precision, bye bye!
			// -----------------------------------------------------
			if(isLocalMinimum(approachData))
			{
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

}
