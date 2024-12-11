using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

public class SupabaseClient
{
    private readonly string SUPABASE_URL = "https://jxahsjtmygsbzlmteuxb.supabase.co";
    private readonly string SUPABASE_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imp4YWhzanRteWdzYnpsbXRldXhiIiwicm9sZSI6ImFub24iLCJpYXQiOjE3MzAxMTE5NjgsImV4cCI6MjA0NTY4Nzk2OH0.S5bZ4kRugCGoC2X4t7aV67jqyRjBZRWvguWyy3h9OL0";

    [Table("user_demographics")]
    public class UserDemographics : BaseModel
    {
        [Column("user_id")]
        public string UserId { get; set; }

        [Column("rejectSaveData")]
        public bool RejectSaveData { get; set; }
    }

    public Supabase.Client client { get; }

    public SupabaseClient()
    {
        client = new Supabase.Client(SUPABASE_URL, SUPABASE_KEY);
    }

    public async Task Init()
    {
        await client.InitializeAsync();
    }

    public async Task<bool> getIsSaveUserData(string userId)
    {
        if (userId == null) return false;

        var result = await client.From<UserDemographics>()
                        .Filter("user_id", Constants.Operator.Equals, userId)
                        .Select("rejectSaveData")
                        .Single();

        if (result == null) return false;

        return !result.RejectSaveData;
    }

    public string GetUserId(string authHeader)
    {
        var token = authHeader?.Split(' ').Last();

        var principal = TokenValidator.ValidateToken(token);
        var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userId;
    }
}