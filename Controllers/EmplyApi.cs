using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DevExpress.Xpo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace crt.Controllers
{
    public class EmplyApi
    {
        private readonly RestClient _client;
        private readonly string _apiKey;
        private readonly string _baseUrl;
        
        public EmplyApi(string customer, string apiKey)
        {
            _baseUrl = $"https://api.emply.com/v1/{customer}/";
            _apiKey = apiKey;
            
            _client = new RestClient(_baseUrl);
            _client.AddDefaultParameter("apiKey", _apiKey, ParameterType.QueryString);
        }
        
        private async Task<OnboardingInfo> GetFormData(OnboardingInfo onboardingInfo, string formId)
        {
            var request = new RestRequest($"onboardings/{onboardingInfo.id}/form-data/{formId}");
            var response = await _client.ExecuteAsync(request);
            
            Console.WriteLine($"Getting onboarding data for '{onboardingInfo.customerName}'...");

            var myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(response.Content);
                
            var formData = new FormData();

            try
            {
                formData.customerName = myDeserializedClass[0].text;
                formData.solution = myDeserializedClass[1].relations[0].department.title;
                formData.partner = myDeserializedClass[2].options.Count > 0 ? myDeserializedClass[2].options[0].optionTitle.localization[0].value : null;
                formData.kickOffDate = myDeserializedClass[3].date.HasValue ? myDeserializedClass[3].date.Value : null;
                formData.goLiveDate = myDeserializedClass[3].date2.HasValue ? myDeserializedClass[3].date2.Value : null;
                formData.crAgent = myDeserializedClass[4].relations.Count > 0 ? myDeserializedClass[4]?.relations[0]?.user : new Employee();
                formData.crAgent2 = myDeserializedClass[5].relations.Count > 0 ? myDeserializedClass[5]?.relations[0]?.user : new Employee();
                formData.salesAgent = myDeserializedClass[6]?.relations[0]?.user ?? new Employee();

            } catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }

            onboardingInfo.formData = formData;

            return onboardingInfo;
        }
        
        private IEnumerable<OnboardingInfo> GetOnboardingInfos(DateTime date)
        {
            var request = new RestRequest("onboardings/find-by-date");
            
            request.AddParameter("date", $"{date:MM-dd-yyyy}");
            
            var response = _client.ExecuteAsync(request).Result.Content;
            
            var onboardingInfos = JsonConvert.DeserializeObject<List<OnboardingInfo>>(response ?? string.Empty);

            return onboardingInfos;
        }
        
        public async Task<IEnumerable<OnboardingInfo>> GetOnboardings(DateTime date, string formid)
        {

            var onboardings = GetOnboardingInfos(date);
            
            var onboardingList = new List<OnboardingInfo>();
            var batchSize = 100;
            int numberOfBatches = (int)Math.Ceiling((double)onboardings.Count() / batchSize);

            for(int i = 0; i < numberOfBatches; i++)
            {
                var currentIds = onboardings.Skip(i * batchSize).Take(batchSize);
                var tasks = currentIds.Select(onboarding => GetFormData(onboarding, formid));
                onboardingList.AddRange(await Task.WhenAll(tasks));
            }
            
            return onboardingList;
        }

        private async Task<Employee> SetEmployeeCapacity(Employee employee)
        {
            string exportId = "a8cfcf13-6921-4712-92f1-39b37d1a2615";
            string formId = "0899bb6c-fb0e-4f83-8cf0-45acc29f4a52";
            var request = new RestRequest($"employees/{employee.id}/form-data/{formId}");
            var response = await _client.ExecuteAsync(request);
            Console.WriteLine("Getting capcity for " + employee.email);

            var json = response.Content;
            
            var myDeserializedClass = JsonConvert.DeserializeObject<List<Root>>(json);
            var capacities = new List<Capacity>();
            
            foreach (var item in myDeserializedClass)
            {
                if (item.titleDataType.localization.Find(x => x.locale == "en-GB") != null)
                {
                    var capacity = new Capacity();
                    capacity.month = item.titleDataType.localization.Find(x => x.locale == "en-GB")?.value;
                    capacity.capacity = item.text;
                    capacities.Add(capacity);
                }
            }
            
            capacities.RemoveAt(0);
            
            employee.capacity = capacities;

            return employee;
        }
        
        private Employee AddOnboardingsToEmployee(Employee employee, IEnumerable<OnboardingInfo> onboardings)
        {
            employee.onboardings = onboardings.Where(o => o.formData.crAgent.email == employee.email || o.formData.crAgent2.email == employee.email);
            return employee;
        }
        
        private List<Employee?> SanitizeEmployees(IEnumerable<Employee?> employees, IEnumerable<OnboardingInfo> onboardings)
        {
            //Add onboardings to employees
            employees = employees.Select(e => AddOnboardingsToEmployee(e, onboardings)).ToList();
        
            //remove employees where status is not active
            employees = employees.Where(e => e.status?.ToLower() == "active").ToList();
        
            //remove employees where there length of onboardings is below 1
            employees = employees.Where(e => e.onboardings.ToList().Count > 0).ToList();
        
            //set fullname on all employees
            foreach (var employee in employees)
            {
                employee?.SetFullName();
            }
        

            return employees.ToList();
        }

        public async Task<IEnumerable<Employee?>> GetEmployees(IEnumerable<OnboardingInfo> onboardings)
        {
            var request = new RestRequest("employees");
            var response = _client.ExecuteAsync(request).Result.Content;
            var employees = JsonConvert.DeserializeObject<List<Employee>>(response ?? string.Empty);
            Console.WriteLine("Getting employees...");

            employees = SanitizeEmployees(employees, onboardings);
            
            
            var newEmployeeList = new List<Employee>();
            var batchSize = 100;
            int numberOfBatches = (int)Math.Ceiling((double)employees.Count() / batchSize);

            for(int i = 0; i < numberOfBatches; i++)
            {
                var currentIds = employees.Skip(i * batchSize).Take(batchSize);
                var tasks = currentIds.Select(employee => SetEmployeeCapacity(employee));
                newEmployeeList.AddRange(await Task.WhenAll(tasks));
            }
            
            return newEmployeeList;
        }
    }
    
    #region Schemas
    public class OnboardingInfo
    {
        public string id {get; set;}
        [JsonProperty("firstName")]
        public string customerName {get; set;}
        public string departmentId {get; set;}
        public int number {get; set;}
        public string created {get; set;}
        public string updated {get; set;}
        public dynamic formData { get; set; }
    }

    public class FormData
    {
        public string customerName {get; set;}
        public string solution {get; set;}
        public string partner {get; set;}
        public DateTime? kickOffDate {get; set;}
        public DateTime? goLiveDate {get; set;}
        public Employee crAgent {get; set;}
        public Employee? crAgent2 {get; set;}
        public Employee salesAgent {get; set;}
    }

    public class Department
    {
        public string id { get; set; }
        public object customId { get; set; }
        public object parentId { get; set; }
        public object parentCustomId { get; set; }
        public string title { get; set; }
        public object externalTitle { get; set; }
        public object managerId { get; set; }
        public object companyName { get; set; }
        public object url { get; set; }
        public bool active { get; set; }
        public object costCenterId { get; set; }
        public DateTime created { get; set; }
        public DateTime updated { get; set; }
        public object deleted { get; set; }
    }

    public class Localization
    {
        public string locale { get; set; }
        public string value { get; set; }
    }

    public class Option
    {
        public string id { get; set; }
        public string optionId { get; set; }
        public OptionTitle optionTitle { get; set; }
    }

    public class OptionTitle
    {
        public List<Localization> localization { get; set; }
    }

    public class Relation
    {
        public string id { get; set; }
        public string userId { get; set; }
        public Employee? user { get; set; }
        public string departmentId { get; set; }
        public Department department { get; set; }
        public object locationId { get; set; }
        public object location { get; set; }
        public object employeeId { get; set; }
        public object employee { get; set; }
        public object absenceGroupId { get; set; }
        public object payrollGroupId { get; set; }
        public object jobProfileId { get; set; }
        public object benefitGroupId { get; set; }
        public object employmentId { get; set; }
        public object benefitTemplateId { get; set; }
        public object supplementAndDeductionTemplateId { get; set; }
        public object pensionSchemeTemplateId { get; set; }
        public object hourlySalaryRateTemplateId { get; set; }
        public object supplementIntervalType { get; set; }
        public object benefitPaymentType { get; set; }
    }

    public class Root
    {
        public string id { get; set; }
        public string text { get; set; }
        public List<object> translations { get; set; }
        public string dataTypeId { get; set; }
        public TitleDataType titleDataType { get; set; }
        public int type { get; set; }
        public object note { get; set; }
        public List<Relation> relations { get; set; }
        public List<Option> options { get; set; }
        public DateTime? date { get; set; }
        public DateTime? date2 { get; set; }
    }

    public class TitleDataType
    {
        public List<Localization> localization { get; set; }
    }

    public class Employee
    {
        public string id { get; set; }
        public string firstName { get; set; }
        public string middleName { get; set; }
        public string lastName { get; set; }
        public string email { get; set; }
        public int number { get; set; }
        public string? status { get; set; }

        public string fullName { get; set; }
        
        public void SetFullName()
        {
            fullName = middleName == null ? $"{firstName} {lastName}" : $"{firstName} {middleName} {lastName}";
        }

        public IEnumerable<OnboardingInfo> FindOnboardingsPerMonth(DateTime date)
        {
            //Find all onboardings for employee where date is same or before goLiveDate, or same or after kickOffDate
            var onboardingsPerMonth = onboardings.Where(onboarding => onboarding.formData.goLiveDate <= date || onboarding.formData.kickOffDate >= date);
            return onboardingsPerMonth;
        }

        public IEnumerable<OnboardingInfo> onboardings { get; set; }
        
        public IEnumerable<Capacity> capacity { get; set; }

        protected bool Equals(Employee other)
        {
            return firstName == other.firstName && email == other.email;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Employee) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(firstName, email);
        }
    }
    
    public class Capacity
    {
        public string month { get; set; }
        public string capacity { get; set; }
    }
    #endregion
}