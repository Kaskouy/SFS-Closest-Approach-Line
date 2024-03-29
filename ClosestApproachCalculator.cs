﻿using SFS.World;
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
		public double sepSpeed;  // the separation speed between the 2 objects (if positive, they are getting further from eachother)
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


	// That part is used to stabilize the closest approach line.
	// If the engines/RCS are off (so the trajectory doesn't change), it's only recalculated every 2 seconds.
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


	// --------------------------------------------
	// Basic methods to calculate the approach data
	// --------------------------------------------

	public static double GetApproachSpeed(T_ApproachData approachData)
    {
		Double2 relativeVelocity = approachData.locPlayer.velocity - approachData.locTarget.velocity;
		return relativeVelocity.magnitude;
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
			approachData.sepSpeed = 0.0;
		}
		else
		{
			Double2 relativeVelocity = approachData.locTarget.velocity - approachData.locPlayer.velocity;
			approachData.sepSpeed = Double2.Dot(relativePos, relativeVelocity) / approachData.dist;
		}

		approachData.validity = true;

		return approachData;
	}

	private static T_ApproachData GetApproachAtArgument(Orbit orbit_A, Orbit orbit_B, double fromTime, double argument)
	{
		const double C_MIN_DISTANCE = 1.0; // Below this threshold, we consider we have a perfect encounter

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

		// Get separation speed
		if (approachData.dist < C_MIN_DISTANCE)
		{
			approachData.sepSpeed = 0.0;
		}
		else
		{
			Double2 relativeVelocity = approachData.locTarget.velocity - approachData.locPlayer.velocity;
			approachData.sepSpeed = Double2.Dot(relativePos, relativeVelocity) / approachData.dist;
		}

		approachData.validity = true;

		return approachData;
	}


	// --------------------------------------------------------------------------------------------------------
	//                            CALCULATION METHODS
	// --------------------------------------------------------------------------------------------------------
	// Here is the tough part of the code, that allows to search for the minimal approach thanks to advanced
	// optimisation methods. The brain of the mod!
	// --------------------------------------------------------------------------------------------------------

	// The dichotomic method: calculates the approach at the middle interval
	// Very safe but not very efficient; used as a last resort if there was a problem with other methods
	// (this is a security, that should never happen in practice)
	private static T_ApproachData getApproachData_DichotomicMethod(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2)
	{
		double time = (approachData1.date + approachData2.date) / 2.0;

		return GetApproachAtDate(orbit_A, orbit_B, time);
	}


	// That function approximates the distance as a grade 3 polynom (thanks to the distance values, and the separation speeds which are the derivatives)
	// and returns the calculated minimum.
	// Usually gives very good results, though it's not that good if the objects are on an encounter trajectory (the linear approximation is way better in this case)
	private static T_ApproachData getApproachData_CubicApproximation(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2, bool forceExactCalculation = false)
    {
		double deltaX = approachData2.date - approachData1.date;
		double p = (approachData2.dist - approachData1.dist) / deltaX;

		double a = 3 * (approachData2.sepSpeed + approachData1.sepSpeed - 2.0 * p) / (deltaX * deltaX);
		double b = 2 * (3.0 * p - approachData2.sepSpeed - 2.0 * approachData1.sepSpeed) / deltaX;
		double c = approachData1.sepSpeed;

		double delta = b * b - 4.0 * a * c;

		if(delta < 0.0)
        {
			// Normally not possible
			//FileLog.Log("getApproachData_CubicApproximation : ERROR : delta < 0.0");
			return getApproachData_DichotomicMethod(orbit_A, orbit_B, approachData1, approachData2); // better than nothing...
		}
        else
        {
			double new_time;

			new_time = approachData1.date + (-b + Math.Sqrt(delta)) / (2.0 * a);

			if ((new_time < approachData1.date) || (new_time > approachData2.date))
            {
				//FileLog.Log("getApproachData_CubicApproximation : ERROR : new time out of range");
				//FileLog.Log("getApproachData_CubicApproximation : A = " + a + "; B = " + b + "; C = " + c);
				//FileLog.Log("getApproachData_CubicApproximation : delta = " + delta);
				//FileLog.Log("getApproachData_CubicApproximation : time1 = " + approachData1.date + "; time2 = " + approachData2.date + "; new time = " + new_time);
				return getApproachData_DichotomicMethod(orbit_A, orbit_B, approachData1, approachData2); // better than nothing...
			}
            else
            {
				double Tau = (new_time - approachData1.date) / (approachData2.date - approachData1.date);

				const double C_THRESHOLD_SMART_CUT_STRATEGY = 0.01;

				if( ((Tau > C_THRESHOLD_SMART_CUT_STRATEGY) && (1.0 - Tau > C_THRESHOLD_SMART_CUT_STRATEGY)) || forceExactCalculation)
                {
					// We return the best approximation possible with this method
					T_ApproachData newApproachData = GetApproachAtDate(orbit_A, orbit_B, new_time);

					//FileLog.Log("getApproachData_CubicApproximation : Time1  = " + approachData1.date + "; Time2 = " + approachData2.date + "; new time = " + newApproachData.date + "; relative pos = " + ((new_time - approachData1.date) / (approachData2.date - approachData1.date)));
					//FileLog.Log("getApproachData_CubicApproximation : Dist1  = " + approachData1.dist + "; Dist2 = " + approachData2.dist + "; new dist = " + newApproachData.dist);
					//FileLog.Log("getApproachData_CubicApproximation : Vel1  = " + approachData1.sepSpeed + "; Vel2 = " + approachData2.sepSpeed + "; new vel = " + newApproachData.sepSpeed);

					return newApproachData;
				}
				else
                {
					// new value is really close from one of the original bounds: the result may not be that great
					// -> Instead of trying to find the best value, we'll greatly reduce the research interval
					return getApproachData_smartCutStrategy(orbit_A, orbit_B, approachData1, approachData2, new_time);
				}
			}
        }
	}

	// That function estimates the point of the minimal approach by assuming that the objects are moving in a straight line.
	// The calculation is simple and gives some very good results in practice, in particular if the objects are on an encounter trajectory.
	// However, it can work pretty bad in some cases. Call isLinearApproximationSuitable before to know if it's suitable to use this method.
	private static T_ApproachData getApproachData_LinearMovementApproximation(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2, bool forceExactCalculation = false)
	{
		// Consists in approximating the movement as a linear one. It's equivalent to neglecting gravity.
		// It's ok if the objects are close to eachother, it can work great in those cases. Otherwise, it can be very bad!

		// Position of target with respect to the player at time 1 and time 2
		Double2 P1 = approachData1.locTarget.position - approachData1.locPlayer.position;
		Double2 P2 = approachData2.locTarget.position - approachData2.locPlayer.position;

		Double2 P1P2 = P2 - P1;

		// Tau is normally between 0 and 1. Tau = 0: new_time is date 1; Tau = 1: new_time is date 2.
		// Tau can be out of bounds in some rare situations. Call isLinearApproximationSuitable to check if it is suitable in a given case.
		double Tau = -Double2.Dot(P1, P1P2) / Math.Pow(P1P2.magnitude, 2.0);

		if (Tau < 0.0) Tau = 0.0;
		if (Tau > 1.0) Tau = 1.0;

		double new_time = approachData1.date + Tau * (approachData2.date - approachData1.date);

		const double C_THRESHOLD_SMART_CUT_STRATEGY = 0.01;

		if ( ((Tau > C_THRESHOLD_SMART_CUT_STRATEGY) && (Tau < 1.0 - C_THRESHOLD_SMART_CUT_STRATEGY)) || forceExactCalculation)
		{
			// We return the best approximation possible with this method
			T_ApproachData newApproachData = GetApproachAtDate(orbit_A, orbit_B, new_time);

			//FileLog.Log("getApproachData_LinearMovementApproximation : Time1 = " + approachData1.date + "; Time2 = " + approachData2.date + "; NewTime = " + newApproachData.date + "; relative pos = " + ((new_time - approachData1.date) / (approachData2.date - approachData1.date)));
			//FileLog.Log("getApproachData_LinearMovementApproximation : Dist1 = " + approachData1.dist + "; Dist2 = " + approachData2.dist + "; NewDist = " + newApproachData.dist);
			//FileLog.Log("getApproachData_LinearMovementApproximation : Vel1  = " + approachData1.sepSpeed + "; Vel2  = " + approachData2.sepSpeed + "; NewVel  = " + newApproachData.sepSpeed);

			return newApproachData;
		}
		else
		{
			// We are really close to one of the bounds: we apply the smart cut strategy instead
			return getApproachData_smartCutStrategy(orbit_A, orbit_B, approachData1, approachData2, new_time);
		}
	}


	// That functions uses the "Smart cut" strategy to locate the point of minimum distance.
	// Instead of trying to find the minimum accurately, it will return a value that will help to greatly reduce the research interval.
	// It's suitable when the minimum is very close to one of the interval bounds, as the other methods gradually lose precision otherwise.
	// When this method is called, the minimum is often found on the next shot in practice.
	private static T_ApproachData getApproachData_smartCutStrategy(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2, double new_time)
    {
		// new value is really close from one of the original bounds: the result may not be that great
		// -> Instead of trying to find the best value, we'll greatly reduce the research interval
		double initial_date;
		double delta_time;
		bool positivesepSpeedExpected;
		double mid_interval_duration = (approachData2.date - approachData1.date) / 2.0;

		if (new_time < approachData1.date + mid_interval_duration)
		{
			// the new time is closer to date 1
			initial_date = approachData1.date;
			delta_time = new_time - approachData1.date;
			positivesepSpeedExpected = true;

			if (delta_time < 0.0) delta_time = 0.0; // As a security if new_time was out of bounds
		}
		else
		{
			// the new time is closer to date 2
			initial_date = approachData2.date;
			delta_time = new_time - approachData2.date; // it's intentionally negative
			positivesepSpeedExpected = false;

			if (delta_time > 0.0) delta_time = 0.0; // As a security if new_time was out of bounds
		}

		// At that stage, the ideal date prediction is equal to initial_date + delta_time
		// It's really close to one of the interval bounds (delta_time is small)
		// Instead of trying to strike on the most precise value, we'll try to reduce the interval length by
		// bringing the opposite bound to a much closer value

		bool ok = false;
		T_ApproachData newApproachData;

		uint nbIterations = 0;
		const uint NB_MAX_ITERATIONS = 5; // to secure the loop

		// This loop should exit on first iteration in practice. 
		do
		{
			nbIterations++;

			// We overshoot by a factor 2 compared to the previous estimation
			delta_time = 2.0 * delta_time;

			// If we pass the middle of the time interval, we limit ourselves to that value and we stop no matters what happens
			// If this happens, it is equivalent to a dichotomic method, though at a high efficiency cost
			if (Math.Abs(delta_time) > mid_interval_duration)
			{
				delta_time = Math.Sign(delta_time) * mid_interval_duration;
				ok = true;
			}

			newApproachData = GetApproachAtDate(orbit_A, orbit_B, initial_date + delta_time);

			if (positivesepSpeedExpected)
			{
				if (newApproachData.sepSpeed > 0) ok = true;
			}
			else
			{
				if (newApproachData.sepSpeed < 0) ok = true;
			}

			if(!ok && (Math.Abs(delta_time) < mid_interval_duration / 16.0) )
            {
				// In case we have to loop again, set a minimal value to delta_time if it's really low to avoid looping too much
				delta_time = Math.Sign(delta_time) * mid_interval_duration / 16.0;
			}

			//FileLog.Log("getApproachData_smartCutStrategy : Apply \"Smart cut\" strategy : result = " + ok);
			//FileLog.Log("getApproachData_smartCutStrategy : Time1  = " + approachData1.date + "; Time2 = " + approachData2.date + "; new time = " + newApproachData.date + "; relative pos = " + ((newApproachData.date - approachData1.date) / (approachData2.date - approachData1.date)));
			//FileLog.Log("getApproachData_smartCutStrategy : Dist1  = " + approachData1.dist + "; Dist2 = " + approachData2.dist + "; new dist = " + newApproachData.dist);
			//FileLog.Log("getApproachData_smartCutStrategy : Vel1   = " + approachData1.sepSpeed + "; Vel2  = " + approachData2.sepSpeed + "; new vel  = " + newApproachData.sepSpeed);
		} while (!ok && (nbIterations < NB_MAX_ITERATIONS));

		return newApproachData;
	}


	// Indicates if the linear approximation is suitable to estimate the minimal approach
	// It simply means that there's an angle of more than 90º between the relative position of objects at date 1 and date 2
	// The linear approximation is a simple calculation, but it happens to work very well in practice if this condition is satisfied
	private static bool isLinearApproximationSuitable(T_ApproachData approachData1, T_ApproachData approachData2)
    {
		Double2 P1 = approachData1.locTarget.position - approachData1.locPlayer.position; // position of target relative to player at date 1
		Double2 P2 = approachData2.locTarget.position - approachData2.locPlayer.position; // position of target relative to player at date 2

		return (Double2.Dot(P1, P2) < -0.0 * approachData1.dist * approachData2.dist);
	}


	private static bool isLocalMinimum(T_ApproachData approachData)
    {
		const double MIN_ABSOLUTE_SPEED = 0.1;    // approach speed must be below this value or...
		const double MIN_RELATIVE_SPEED = 0.0001; // approach speed must be below this value times the relative speed between target and player

		double relativeVelocity = (approachData.locPlayer.velocity - approachData.locTarget.velocity).magnitude;

		if ( (Math.Abs(approachData.sepSpeed) < MIN_ABSOLUTE_SPEED) || 
			 (Math.Abs(approachData.sepSpeed) < MIN_RELATIVE_SPEED * relativeVelocity) )
		{
			return true;
		}
		else
        {
			return false;
        }
	}


	// This function is used to calculate the accurate minimal approach between 2 dates relatively close to eachother, so that
	// the distance variation is relatively regular. To work, the algorithm supposes that:
	// - distance between objects is decreasing at date 1 (separation speed negative)
	// - distance between objects is increasing at date 2 (separation speed positive)
	// Otherwise the function returns the best of the 2 points, or invalid data.
	private static T_ApproachData calculateMinimalApproachBetweenDates(Orbit orbit_A, Orbit orbit_B, T_ApproachData approachData1, T_ApproachData approachData2, double current_min_dist = -1.0)
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

		// CHECK SPEEDS
		// ------------
		if ((approachData1.sepSpeed > 0.0) || (approachData2.sepSpeed < 0.0))
        {
			// Speed 1 is supposed to be negative, and speed 2 is supposed to be positive.
			// The algorithm can't work as there's no guaranteed minimum so we return the best of both points
			if (approachData1.dist < approachData2.dist)
			{
				//FileLog.Log("  CALCULATE APPROACH DATA : relative speeds are inadequate -> return point 1");
				return approachData1;
			}
			else
			{
				//FileLog.Log("  CALCULATE APPROACH DATA : relative speeds are inadequate -> return point 2");
				return approachData2;
			}
		}

		// CHECK IF START/END POINT ARE A MINIMUM
		// --------------------------------------
		if (isLocalMinimum(approachData1))
		{
			//FileLog.Log("  CALCULATE APPROACH DATA : Point 1 is a minimum");
			//FileLog.Log("calculateMinimalApproachBetweenDates : Time = " + approachData1.date + "; Dist = " + approachData1.dist + "; Vel = " + approachData1.sepSpeed);
			return approachData1;
		}

		if (isLocalMinimum(approachData2))
		{
			//FileLog.Log("  CALCULATE APPROACH DATA : Point 2 is a minimum");
			//FileLog.Log("calculateMinimalApproachBetweenDates : Time = " + approachData2.date + "; Dist = " + approachData2.dist + "; Vel = " + approachData2.sepSpeed);
			return approachData2;
		}

		// ACCURATE MINIMUM CALCULATION
		// ----------------------------
		// Now we are sure that:
		// - T2 > T1
		// - approachData1.sepSpeed < 0.0: the objects are approaching at date 1
		// - approachData2.sepSpeed > 0.0: the objects are getting further at date 2
		// => There is a minimal approach between those 2 dates

		const uint MAX_ITERATIONS = 15;
		const double C_MIN_DURATION_INTERVAL = 10.0;
		uint nbIterations = 0;
		bool force_exact_calculation = false;

		T_ApproachData loc_ApproachData1 = approachData1;
		T_ApproachData loc_ApproachData2 = approachData2;

		//FileLog.Log("  CALCULATE APPROACH DATA");

		while (nbIterations < MAX_ITERATIONS)
		{
			nbIterations++;

			// Calculate next approximation
			// ----------------------------
			if(loc_ApproachData2.date - loc_ApproachData1.date < C_MIN_DURATION_INTERVAL)
            {
				// If the minimum is within a time interval of less than this, skip a possible "smart cut" move and aim directly for a precise result
				// This is to keep the calculations at an acceptable level in extreme situations (close encounter at high speed)
				//FileLog.Log("Step " + nbIterations + ": Force precise result");
				force_exact_calculation = true;
			}
			
			if(isLinearApproximationSuitable(loc_ApproachData1, loc_ApproachData2))
			{
				// Objects are close to eachother during the whole time interval, so it's possible to neglect the difference of gravity between them
				// The linear approximation is very efficient and gives excellent results in this case
				//FileLog.Log("Step " + nbIterations + ": Calculate linear aproximation");
				approachData = getApproachData_LinearMovementApproximation(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2, force_exact_calculation);
			}
			else
			{
				// Otherwise, use the more general cubic approximation
				//FileLog.Log("Step " + nbIterations + ": Calculate cubic aproximation");
				approachData = getApproachData_CubicApproximation(orbit_A, orbit_B, loc_ApproachData1, loc_ApproachData2, force_exact_calculation);
			}

			// If answer found with a satisfying precision, bye bye!
			// -----------------------------------------------------
			if (force_exact_calculation || isLocalMinimum(approachData))
			{
				return approachData;
			}

			// Otherwise, prepare data for the next iteration
			// ----------------------------------------------
			if (approachData.sepSpeed < 0.0)
			{
				loc_ApproachData1 = approachData; // for next iteration, calculated approach data replaces value 1
			}
			else
			{
				loc_ApproachData2 = approachData; // for next iteration, calculated approach data replaces value 2
			}

			// Give up early if we are too far from a previously found minimum
			// ---------------------------------------------------------------
			if( (nbIterations == 1) && (current_min_dist > 0.0) && (approachData.dist > 2.0 * current_min_dist) )
            {
				// If there is already a minimum distance specified, check that we are not too far from it (otherwise give up)
				//FileLog.Log("calculateMinimalApproachBetweenDates : current distance (" + approachData.dist + ") too far from the minimum (" + current_min_dist + ") - Give up!");
				approachData.validity = false;
				return approachData;
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

	private static T_ApproachData CalculateClosestApproachOnPeriod(Orbit orbit_A, Orbit orbit_B, double time_start, double time_end, double ref_period)
	{
		T_ApproachData approachData_globalMin = new T_ApproachData();
		approachData_globalMin.dist = -1.0; // It's important to initialize it to a negative value, so that if no valid value replaced it, it's ignored when passed to calculateMinimalApproachBetweenDates
		approachData_globalMin.validity = false;

		// Sampling a few points
		// ---------------------
		const uint C_NB_TIME_INTERVALS_PER_REF_PERIOD = 12;
		uint nbPoints = (uint)((time_end - time_start) / ref_period * C_NB_TIME_INTERVALS_PER_REF_PERIOD) + 2;

		// Get a list of points
		//FileLog.Log("\n   CALCULATE MINIMAL APPROACH ON PERIOD: " + time_start + " - " + time_end + " - SAMPLING " + nbPoints + " points...");
		T_ApproachData[] tab_ApproachData = GetApproachDataSamples(orbit_A, orbit_B, time_start, time_end, nbPoints);

		//FileLog.Log("List of points:");
		//for (int i = 0; i < tab_ApproachData.Length; i++) FileLog.Log("Point " + i + ": date = " + tab_ApproachData[i].date + "; dist = " + tab_ApproachData[i].dist + "; vel = " + tab_ApproachData[i].sepSpeed);


		// Then locate point where distance is minimum
		// -------------------------------------------
		int i_min = 0;
		double bestDistance = tab_ApproachData[0].dist;

		for (int i = 1; i < tab_ApproachData.Length; i++)
        {
			if (tab_ApproachData[i].dist < bestDistance)
			{
				i_min = i;
				bestDistance = tab_ApproachData[i].dist;
			}
		}

		//FileLog.Log("CalculateClosestApproachOnPeriod: Best point from sample is point " + i_min + " at date = " + tab_ApproachData[i_min].date + "; dist = " + tab_ApproachData[i_min].dist + "; rel speed = " + tab_ApproachData[i_min].sepSpeed);

		if ((i_min > 0) && (tab_ApproachData[i_min].sepSpeed > 0.0))
        {
			// if separation speed is positive, we are past the minimum
			// --> we substract 1 so that the potential minimum is always between i_min and i_min + 1
			i_min--;
        }

		// if separation speed decreases on the last point, this is a potential minimum: initialize the global min with it
		// Note: this is the only case in which we accept a point that is not a local minimum as the global minimum
		if (tab_ApproachData[tab_ApproachData.Length - 1].sepSpeed < 0.0)
		{
			approachData_globalMin = tab_ApproachData[tab_ApproachData.Length - 1];
		}

		// Calculate the minimum between the 2 best points (i.e. the best point and either its follower or its predecessor)
		// -----------------------------------------------
		if ((i_min < tab_ApproachData.Length - 1) && (tab_ApproachData[i_min].sepSpeed < 0.0) && (tab_ApproachData[i_min+1].sepSpeed > 0.0))
        {
			//FileLog.Log("CalculateClosestApproachOnPeriod: Calculating closest approach between the best points: " + i_min + " and " + (i_min + 1));
			//FileLog.Log("CalculateClosestApproachOnPeriod: Point 1: date = " + tab_ApproachData[i_min].date + "; dist = " + tab_ApproachData[i_min].dist + "; vel = " + tab_ApproachData[i_min].sepSpeed);
			//FileLog.Log("CalculateClosestApproachOnPeriod: Point 2: date = " + tab_ApproachData[i_min + 1].date + "; dist = " + tab_ApproachData[i_min + 1].dist + "; vel = " + tab_ApproachData[i_min + 1].sepSpeed);

			// The minimum calculated between the 2 best points is often the best in the end --> we start by that one to optimize the calculations that come next
			T_ApproachData approachData_localMin = calculateMinimalApproachBetweenDates(orbit_A, orbit_B, tab_ApproachData[i_min], tab_ApproachData[i_min + 1], approachData_globalMin.dist);

			if (approachData_localMin.validity)
			{
				// No global minimum defined, or global minimum is not as good --> memorize the new value as the best
				if (!approachData_globalMin.validity || (approachData_localMin.dist < approachData_globalMin.dist))
				{
					approachData_globalMin = approachData_localMin;
				}
			}
		}


		// Then search all others local minima to find the best one
		// --------------------------------------------------------
		for (int i = 0; i < tab_ApproachData.Length - 1; i++)
        {
			if( (i != i_min) && (tab_ApproachData[i].sepSpeed < 0.0) && (tab_ApproachData[i+1].sepSpeed > 0.0)) // i_min is excluded since we already calculated it
			{
				// Objects are approaching at date 1, moving away at date 2 -> we have a local minimum between those 2 dates
				T_ApproachData approachData_localMin = calculateMinimalApproachBetweenDates(orbit_A, orbit_B, tab_ApproachData[i], tab_ApproachData[i+1], approachData_globalMin.dist);

				if (approachData_localMin.validity)
				{
					// No global minimum defined, or global minimum is not as good --> memorize the new value as the best
					if (!approachData_globalMin.validity || (approachData_localMin.dist < approachData_globalMin.dist))
					{
						approachData_globalMin = approachData_localMin;
					}
				}
			}
		}

		//if(approachData_globalMin.validity) FileLog.Log("CalculateClosestApproachOnPeriod: Found global minimum at date = " + approachData_globalMin.date + "; dist = " + approachData_globalMin.dist);
		//else FileLog.Log("CalculateClosestApproachOnPeriod: No satisfying minimum found!");
		
		return approachData_globalMin;
	}

	// ---------------------------------------------------------------------------------------------
	// ------                         CalculateClosestApproach                               -------
	// ---------------------------------------------------------------------------------------------
	// This function calculates the closest approach on the current turn (the traditional blue line)
	// Two calculation modes are used:
	// - the closest approach in the mathematical sense: more precise but needs more calculations
	// - the approach at node: more simple
	// The first approach is used if the orbits are both periodic and relatively close, while the
	// second approach is used in other cases.
	// ---------------------------------------------------------------------------------------------
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

			double final_time = Math.Min(start_time + 0.998 * orbit_A.period, end_time);

			T_ApproachData bestApproachData = new T_ApproachData();
			bestApproachData.validity = false;
			bestApproachData.dist = Double.PositiveInfinity;

			bestApproachData = CalculateClosestApproachOnPeriod(orbit_A, orbit_B, start_time, final_time, min_period);

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


	// ---------------------------------------------------------------------------------------------
	// ------                   CalculateClosestApproachOnShortPeriod                        -------
	// ---------------------------------------------------------------------------------------------
	// This function calculates the closest approach on a short duration from the current date.
	// This is intended to parameterize the velocity arrow during the final approach.
	// The calculation of the closest approach is very simplified, because we assume the research is
	// made on a small period of time.
	// ---------------------------------------------------------------------------------------------
	public static T_ApproachData CalculateClosestApproachOnShortPeriod(Orbit orbit_A, Orbit orbit_B, double start_time, double end_time)
    {
		// VALIDITY CONTROLS
		// -----------------
		// Check if conditions are valid: both orbits are defined in the time period specified
		if ((start_time < orbit_A.orbitStartTime) || (start_time < orbit_B.orbitStartTime))
		{
			// One of the orbit only starts in the future; return invalid value
			T_ApproachData approachData = new T_ApproachData();
			approachData.validity = false;
			return approachData;
		}

		// Cap the end time value in the case where the orbits would end soon
		double actual_end_time = end_time;

		if(actual_end_time > orbit_A.orbitEndTime)
        {
			actual_end_time = orbit_A.orbitEndTime;
        }

		if (actual_end_time > orbit_B.orbitEndTime)
		{
			actual_end_time = orbit_B.orbitEndTime;
		}

		// Check that dates are consistent
		if (start_time > actual_end_time)
		{
			T_ApproachData approachData = new T_ApproachData();
			approachData.validity = false;
			return approachData;
		}

		// CALCULATE CLOSEST APPROACH
		// --------------------------
		// Get the approach data at start and end time
		T_ApproachData approachData_start = GetApproachAtDate(orbit_A, orbit_B, start_time);
		T_ApproachData approachData_end = GetApproachAtDate(orbit_A, orbit_B, actual_end_time);

		if( (approachData_start.sepSpeed > 0.0) || (approachData_end.sepSpeed < 0.0) )
        {
			// No minimum in the specified interval; return invalid value
			T_ApproachData approachData = new T_ApproachData();
			approachData.validity = false;
			return approachData;
		}

		// A minimum exists: calculate and returns it directly
		double relativeSpeed = (approachData_start.locTarget.velocity - approachData_start.locPlayer.velocity).magnitude;
		double distance = (approachData_start.locTarget.position - approachData_start.locPlayer.position).magnitude;

		if ( (relativeSpeed <  20.0) && (distance < 2000.0)) 
        {
			// At close distance and low speed, we apply a single linear approximation
			// (this is because very low speeds tend to make skip the calculation since the separation speed is practically null then)
			return getApproachData_LinearMovementApproximation(orbit_A, orbit_B, approachData_start, approachData_end, true);
		}
        else 
		{
			return calculateMinimalApproachBetweenDates(orbit_A, orbit_B, approachData_start, approachData_end);
		}
	}


	// -----------------------------------------------------------------------------------------------------
	// FUNCTIONS FOR THE MULTI-TURNS APPROACH
	// -----------------------------------------------------------------------------------------------------

	private static bool MustCalculateMultiTurnApproachAtnode(double nodeArg, T_ApproachData closestApproach)
    {
		const double MIN_ANGLE_DIFFERENCE = 3.0 * Math.PI / 180.0; // 3 degrees

		if (closestApproach.validity == false)
        {
			// No valid closest approach --> calculation to be done
			return true;
        }

		// If the closest approach is valid, we check if the approach is located close to the node
		// if yes, we inhibit the multi-turn calculation, so that the lines don't overlap and to let the player
		// deal with the traditional closest approach line without interference
		double argClosestApproachPlayer = closestApproach.locPlayer.position.AngleRadians;
		double argClosestApproachTarget = closestApproach.locTarget.position.AngleRadians;

		// calculate the angle difference between the closest approach arguments and the node
		double argumentDiffPlayer = nodeArg - argClosestApproachPlayer;
		double argumentDiffTarget = nodeArg - argClosestApproachTarget;

		// Normalize angles
		if (argumentDiffPlayer > Math.PI) argumentDiffPlayer -= 2.0 * Math.PI;
		if (argumentDiffPlayer < -Math.PI) argumentDiffPlayer += 2.0 * Math.PI;
		if (argumentDiffTarget > Math.PI) argumentDiffTarget -= 2.0 * Math.PI;
		if (argumentDiffTarget < -Math.PI) argumentDiffTarget += 2.0 * Math.PI;

		// inhibit calculations if the closest approach arguments are too close from the node
		if ((Math.Abs(argumentDiffPlayer) < MIN_ANGLE_DIFFERENCE) && (Math.Abs(argumentDiffTarget) < MIN_ANGLE_DIFFERENCE))
		{
			return false;
		}
        else
        {
			return true;
        }
	}

	public static void CalculateClosestApproach_MultiTurn(Orbit orbit_A, Orbit orbit_B, T_ApproachData closestApproach, out T_ApproachData bestApproachData1, out uint nbTurns1, out T_ApproachData bestApproachData2, out uint nbTurns2)
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

		const uint NB_MAX_TURNS = 12;

		double weightedBestApproach1 = 0, weightedBestApproach2 = 0;
		double curStartTime = start_time + orbit_A.period; // to start after one period

		// Initialization
		bestApproachData1 = new T_ApproachData();
		bestApproachData2 = new T_ApproachData();
		bestApproachData1.validity = false;
		bestApproachData2.validity = false;
		nbTurns1 = nbTurns2 = 0;

		bool calculateApproach1 = MustCalculateMultiTurnApproachAtnode(angleIntersection1, closestApproach);
		bool calculateApproach2 = MustCalculateMultiTurnApproachAtnode(angleIntersection2, closestApproach);

		// Calculate the min distance threshold: if an encounter is found at a distance less than
		// that distance times the number of turns, then we consider it sufficiently interesting
		// and we stop searching alternatives with even more turns
		const double ANGLE_THRESHOLD = 5.0 * Math.PI / 180.0; // 5 degrees
		double distanceMin1 = orbit_A.GetRadiusAtAngle(angleIntersection1) * ANGLE_THRESHOLD;
		double distanceMin2 = orbit_A.GetRadiusAtAngle(angleIntersection2) * ANGLE_THRESHOLD;

		// Calculate approach at node 1 on each turn and memorize the best
		// ---------------------------------------------------------------
		for (uint i_turn = 2; calculateApproach1 && (i_turn <= NB_MAX_TURNS); i_turn++)
		{
			T_ApproachData curApproachData1 = GetApproachAtArgument(orbit_A, orbit_B, curStartTime, angleIntersection1);

			if (i_turn == 2)
			{
				// first iteration
				nbTurns1 = i_turn;
				weightedBestApproach1 = Math.Sqrt(i_turn) * curApproachData1.dist;
				//weightedBestApproach1 = i_turn * curApproachData1.dist;
				bestApproachData1 = curApproachData1;

				// If the distance found is lower than a threshold (dependant on number of turns), then stop there
				if(bestApproachData1.dist < nbTurns1 * distanceMin1) calculateApproach1 = false;
			}
			else
			{
				double cur_weightedBestApproach1 = Math.Sqrt(i_turn) * curApproachData1.dist;
				//double cur_weightedBestApproach1 = i_turn * curApproachData1.dist;

				if (cur_weightedBestApproach1 < weightedBestApproach1)
				{
					weightedBestApproach1 = cur_weightedBestApproach1;
					nbTurns1 = i_turn;
					bestApproachData1 = curApproachData1;

					// If the distance found is lower than a threshold (dependant on number of turns), then stop there
					if (bestApproachData1.dist < nbTurns1 * distanceMin1) calculateApproach1 = false;
				}
			}

			// For next loop: search for the next revolution
			curStartTime += orbit_A.period;
		}

		// reinitialize this variable
		curStartTime = start_time + orbit_A.period;

		// Calculate approach at node 2 on each turn and memorize the best
		// ---------------------------------------------------------------
		for (uint i_turn = 2; calculateApproach2 && (i_turn <= NB_MAX_TURNS); i_turn++)
		{
			T_ApproachData curApproachData2 = GetApproachAtArgument(orbit_A, orbit_B, curStartTime, angleIntersection2);

			if (i_turn == 2)
			{
				// first iteration
				nbTurns2 = i_turn;
				weightedBestApproach2 = Math.Sqrt(i_turn) * curApproachData2.dist;
				//weightedBestApproach2 = i_turn * curApproachData2.dist;
				bestApproachData2 = curApproachData2;

				// If the distance found is lower than a threshold (dependant on number of turns), then stop there
				if (bestApproachData2.dist < nbTurns2 * distanceMin2) calculateApproach2 = false;
			}
			else
			{
				double cur_weightedBestApproach2 = Math.Sqrt(i_turn) * curApproachData2.dist;
				//double cur_weightedBestApproach2 = i_turn * curApproachData2.dist;

				if (cur_weightedBestApproach2 < weightedBestApproach2)
				{
					weightedBestApproach2 = cur_weightedBestApproach2;
					nbTurns2 = i_turn;
					bestApproachData2 = curApproachData2;

					// If the distance found is lower than a threshold (dependant on number of turns), then stop there
					if (bestApproachData2.dist < nbTurns2 * distanceMin2) calculateApproach2 = false;
				}
			}

			// For next loop: search for the next revolution
			curStartTime += orbit_A.period;
		}

		memorizeLastApproachData_MultiTurn(bestApproachData1, nbTurns1, bestApproachData2, nbTurns2);
	}

}
