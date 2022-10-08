using System.Collections;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RestSharp;
using System.Threading;

namespace crt.Controllers;

[ApiController]
[Route("[controller]")]
public class ForecastController : ControllerBase
{
    private const string onboardingFormId = "";
    private const string CRT_API_KEY = "";
    private const string HR_API_KEY = "";
    private readonly ILogger<ForecastController> _logger;

    public ForecastController(ILogger<ForecastController> logger)
    {
        _logger = logger;
    }

    
    [HttpGet]
    public async Task<Forecast> Get()
    {
        //Get date from first in current month
        var date = new DateTime(DateTime.Now.Year, 1, 1);
        //Get first of current month
        var startMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        //Get the month in 12 months
        var endMonth = startMonth.AddMonths(12);

        
        //Get onboardings from CRT API
        var onboardings = await new EmplyApi("crt", CRT_API_KEY).GetOnboardings(date, onboardingFormId);
        //Get employees from HR API
        var employees = await new EmplyApi("hr", HR_API_KEY).GetEmployees(onboardings);
        
        //Remove onboardings where goLiveDate is passed
        onboardings = onboardings.Where(o => o.formData.goLiveDate > DateTime.Now).ToList();
        
        //Create list with startMonth and endMonth
        var months = new List<DateTime>();
        for (var i = startMonth; i <= endMonth; i = i.AddMonths(1))
        {
            months.Add(i);
        }

        //Create object for use in frontend
        var forecast = new Forecast(onboardings, employees, months);

        //Return the object to the frontend
        return forecast;

    }
}