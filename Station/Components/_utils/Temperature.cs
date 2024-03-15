using System;
using LibreHardwareMonitor.Hardware;
using Sentry;
using Station.Components._notification;

namespace Station.Components._utils;

public class Temperature
{
    public class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }
        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware subHardware in hardware.SubHardware) subHardware.Accept(this);
        }
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }

    static public float? GetTemperature()
    {
        try
        {

            Computer computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };

            float? maxTemp = 0;

            computer.Reset();
            computer.Open();
            computer.Accept(new UpdateVisitor());

            foreach (IHardware hardware in computer.Hardware)
            {
                // Console.WriteLine("Hardware: {0}", hardware.Name);

                foreach (IHardware subhardware in hardware.SubHardware)
                {
                    // Console.WriteLine("\tSubhardware: {0}", subhardware.Name);

                    foreach (ISensor sensor in subhardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            // there looks to be a bug somewhere, where occasionally sensors
                            // just report 100.0 degrees constantly. We will just throw
                            // out this result, because if there is a genuine problem
                            // it should vary around 100.0, not just bang on 100.0
                            if (sensor.Value > maxTemp && sensor.Value != 100 && !sensor.Name.Contains("Hot Spot"))
                            {
                                maxTemp = sensor.Value;
                            }
                        }
                    }
                }

                foreach (ISensor sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Temperature)
                    {
                        if (sensor.Value > maxTemp && sensor.Value != 100 && !sensor.Name.Contains("Hot Spot"))
                        {
                            maxTemp = sensor.Value;
                        }
                    }
                }
            }

            computer.Close();
            return maxTemp;
        }
        catch (NullReferenceException)
        {
            return 0;
        }
        catch (Exception e)
        {
            Logger.WriteLog($"GetTemperature - Sentry Exception: {e}", MockConsole.LogLevel.Error);
            SentrySdk.CaptureException(e);
            return 0;
        }
    }
}
