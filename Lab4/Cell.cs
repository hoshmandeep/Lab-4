/* Lab Question (Test 2) 
 * 
 * Here we have used a List to hold the phonons. Given that we will need to remove phonons
 * from many different locations of the List (front, middle, back) do you think
 * this is an appropriate data structure to use? Keep in mind, we will also need repeatedly
 * iterate over the List and the List could contain many phonons. Random access is not required.
 * Justify your choice of a different data structure or explain why a List is a good choice.
 * 
 * Can you think of a clever way to remove an element from the middle of a List without having
 * to shift the memory contents of the List? 
 */
 
using System;
using System.Collections.Generic;

using Psim.Geometry2D;
using Psim.Particles;
using Psim.Materials;

namespace Psim.ModelComponents
{

	public enum SurfaceLocation
	{
		left = 0,
		top = 1,
		right = 2,
		bot = 3
	}

	public class Cell : Rectangle
	{ 
		private const int NUM_SURFACES = 4;
		private List<Phonon> phonons = new() { };
		private List<Phonon> incomingPhonons = new() { };
		private ISurface[] surfaces = new ISurface[NUM_SURFACES];
		public List<Phonon> Phonons { get { return phonons; } }

		public Cell(double length, double width, Material material, double initTemp) : base(length, width)
		{
			for (int i = 0; i < NUM_SURFACES; ++i)
			{
				surfaces[i] = new BoundarySurface((SurfaceLocation)i, this);
			}
			Material = material;
			InitTemp = initTemp;
			BaseTable = material.BaseData(initTemp, out heatCapacity);
			ScatterTable = material.ScatterTable(initTemp);
			Temperature = initTemp;
		}

		/// <summary>
		/// Adds a phonon to the main phonon 'array' of the cell.
		/// </summary>
		/// <param name="p">The phonon that will be added</param>
		public void AddPhonon(Phonon p)
		{
			phonons.Add(p);
		}

		/// <summary>
		/// Adds a phonon to the incoming phonon 'array' of the cell
		/// The incoming phonon will come from the phonons 'array' of another cell
		/// </summary>
		/// <param name="p">The phonon that will be added</param>
		public void AddIncPhonon(Phonon p)
		{
			// This could cause a very nasty bug later on - be sure to verify now
			incomingPhonons.Add(p);
		}

		/// <summary>
		/// Merges the incoming phonons with the existing phonons and clears the incoming phonons
		/// </summary>
		public void MergeIncPhonons()
		{
			phonons.AddRange(incomingPhonons);
			incomingPhonons.Clear();
		}

		/// <summary>
		/// Returns the surface at SurfaceLocation loc
		/// </summary>
		/// <param name="loc">The SurfaceLocation of the surface to be returned</param>
		/// <returns>The surface at location loc</returns>
		public ISurface GetSurface(SurfaceLocation loc)
		{
			return surfaces[(int)loc];
		}

		/// <summary>
		/// Moves a phonon to the surface that it will impact first.
		/// The phonon will be moved to the surface and the surface!!!!!
		/// it impacts is returned
		/// </summary>
		/// <param name="p">The phonon to be moved</param>
		/// <returns>The surface that the phonon collides with or null if it doesnt impact surface</returns>
		public SurfaceLocation? MoveToNearestSurface(Phonon p)
		{
			// Returns the time taken to for a phonon to move back into the cell or 0 if the phonon did not exit the cell
			double GetTime(double dist, double pos, double vel)
			{
				if (pos <= 0) { return pos / vel; } // pos is negative therefore vel must be negative
				else if (pos >= dist) { return (pos - dist) / vel; } // pos is + therefore vel is + and len < pos
				else return 0; // No surface was reached
			}

			p.Drift(p.DriftTime);
			p.GetCoords(out double px, out double py);
			p.GetDirection(out double dx, out double dy);
			double vx = dx * p.Speed;
			double vy = dy * p.Speed;

			// The longer the time, the sooner the phonon impacted the corresponding surface
			double timeToSurfaceX = (vx != 0) ? GetTime(Length, px, vx) : 0;
			double timeToSurfaceY = (vy != 0) ? GetTime(Width, py, vy) : 0;

			// Time needed to backtrack the phonon to the first surface collision
			double backtrackTime = Math.Max(timeToSurfaceX, timeToSurfaceY);
			p.DriftTime = backtrackTime;
			if (backtrackTime == 0) { return null; } // The phonon did not collide with a surface
			p.Drift(-backtrackTime);

			// Miminize FP errors and determine impacted surface
			if (backtrackTime == timeToSurfaceX)
			{
				if (vx < 0)
				{
					p.SetCoords(0, null);
					return SurfaceLocation.left;
				}
				else
					p.SetCoords(Length, null);
				return SurfaceLocation.right;
			}
			else
			{
				if (vy < 0)
				{
					p.SetCoords(null, 0);
					return SurfaceLocation.bot;
				}
				else
				{
					p.SetCoords(null, Width);
					return SurfaceLocation.top;
				}
			}
		}

		public override string ToString()
		{
			return string.Format("{0,-5} {1,-7} {2,-7}", Math.Round(Temperature, 2), phonons.Count, incomingPhonons.Count);
		}

		private double heatCapacity;
		private List<double> temperatures = new() { };
		private List<double> xFluxes = new() { };
		private List<double> yFluxes = new() { };
		public int ID { get; }
		public double InitTemp { get; }
		public Material Material { get; }
		public Tuple<double, double>[] BaseTable { get; private set; }
		public Tuple<double, double>[] ScatterTable { get; private set; }
		public double HeatCapacity { get { return heatCapacity; } }
		public double Temperature { get; private set; }
		public double AreaCovered { get; private set; }

		public void AddToArea(double area) => AreaCovered += area;
		public Tuple<double, double>[] GetEmitData(double temp, out double energy)
		{
			return Material.EmitData(temp, out energy);
		}

		public void TakeMeasurements(double effEnergy, double tEq)
		{
			int energyUnits = 0;
			double xFlux = 0;
			double yFlux = 0;
			foreach (var p in phonons)
			{
				int sign = p.Sign;
				p.GetDirection(out double dx, out double dy);
				energyUnits += sign;
				xFlux += dx * p.Speed * sign;
				yFlux += dy * p.Speed * sign;
			}
			double fluxFactor = effEnergy / AreaCovered;

			temperatures.Add((energyUnits * effEnergy / (AreaCovered * HeatCapacity)) + tEq);
			xFluxes.Add(fluxFactor * xFlux);
			yFluxes.Add(fluxFactor * yFlux);
			UpdateParams();
		}

		public SensorMeasurements GetMeasurements()
		{
			SensorMeasurements measurements;
			measurements.InitTemp = InitTemp;
			measurements.Temperatures = temperatures;
			measurements.XFluxes = xFluxes;
			measurements.YFluxes = yFluxes;
			return measurements;
		}

		private void UpdateParams()
		{
			Temperature = temperatures[^1];
			BaseTable = Material.BaseData(Temperature, out heatCapacity);
			ScatterTable = Material.ScatterTable(Temperature);
		}
	}
}
