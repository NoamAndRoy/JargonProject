using System.Security.Claims;
using Supabase.Postgrest;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

public class SupabaseClient
{
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
        string supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
        string supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");
        client = new Supabase.Client(supabaseUrl, supabaseKey);
    }

    public async Task Init()
    {
        await client.InitializeAsync();
    }

    public async Task<bool> getIsSaveUserData(string? userId)
    {
        if (userId == null) return false;

        var result = await client.From<UserDemographics>()
                        .Filter("user_id", Constants.Operator.Equals, userId)
                        .Select("rejectSaveData")
                        .Single();

        if (result == null) return false;

        return !result.RejectSaveData;
    }
}