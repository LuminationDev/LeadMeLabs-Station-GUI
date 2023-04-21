using LibreHardwareMonitor.Hardware;
using Sentry;
using System;

namespace Station
{
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
                                    Logger.WriteLog($"\t\tSensor: {sensor.Name}, value: {sensor.Value}, type: {sensor.SensorType}", MockConsole.LogLevel.Debug);
                                }
                            }
                        }
                    }

                    foreach (ISensor sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature)
                        {
                            if (sensor.Value > maxTemp)
                            {
                                maxTemp = sensor.Value;
                            }
                            // Console.WriteLine("\t\tSensor: {0}, value: {1}, type: {2}", sensor.Name, sensor.Value, sensor.SensorType);
                        }
                    }
                }

                computer.Close();
                return maxTemp;
            }
            catch (NullReferenceException e)
            {
                return 0;
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                return 0;
            }
        }
    }
}
