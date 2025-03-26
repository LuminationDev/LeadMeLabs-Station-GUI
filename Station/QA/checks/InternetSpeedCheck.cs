namespace Station.QA.checks;

public class InternetSpeedCheck
{
    public QaCheck RunInternetSpeedTest()
    {
        QaCheck qaCheck = new QaCheck("internet_speedtest");
        double result = LeadMeLabsLibrary.InternetSpeedtest.GetInternetSpeed();
        if (result < 0)
        {
            qaCheck.SetFailed("Internet is not accessible.");
            return qaCheck;
        }

        if (result < 10)
        {
            qaCheck.SetWarning($"Internet speed is slow. Speed: {result:N2}Mbps");
            return qaCheck;
        }
        qaCheck.SetPassed($"Internet speed is {result:N2}Mbps");
        return qaCheck;
    }
}