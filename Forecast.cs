using crt.Controllers;

namespace crt;

public class Forecast
{
    public IEnumerable<OnboardingInfo> onboardings { get; set; }
    public IEnumerable<Employee> employees { get; set; }
    public List<DateTime> dates { get; set; }

    public Forecast(IEnumerable<OnboardingInfo> onboardings, IEnumerable<Employee> employees, List<DateTime> dates)
    {
        this.onboardings = onboardings;
        this.employees = employees;
        this.dates = dates;
    }
}