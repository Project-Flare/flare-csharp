using Xunit;
using System;
using flare_csharp;
using System.Threading;
using static flare_csharp.ClientManager;
using System.Net;
using System.Net.NetworkInformation;

namespace Client_Tests
{
    public class Client_Tests
    {

        [Theory]
        [InlineData("llllllllllllllllllllllllllllllllllllllllllllllllllllllllll78", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("HobGoblinasssssssssssssssssssssssssssssssssssssss", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("LowBobasssssssssssssssssssssssssssssssssssssssssssssssss", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("HobGoblsasasgasasinasssssssssssssssssssssssssssssssssssssss", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Kreveteeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "dscbhbaerbyuifb896Q32GQBWHB")]
        public async void Registration_failed_given_username_with_out_of_bounds_length(string username1, string pin1)
        {      

            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username1; 
            manager.Password = pin1;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }
        [Theory]
        [InlineData("Hob‹oblinasß45", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Gobl£ingo‘nas42£", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Ho’bGoblinas42©", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("LowBob‘as42©", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Low£Bobas42©", "dscbhbaerbyuifb896Q32GQBWHB")]
        public async Task Registration_prevented_given_username_containing_non_ASCII_characters(string username2, string pin2)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username2;
            manager.Password = pin2;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }

        [Theory]
        [InlineData("GerasSlaptazodis!", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Blogas$Slaptazodis#", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("#$!'[]", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Yep()keturi*", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("NeVienas(,)", "dscbhbaerbyuifb896Q32GQBWHB")]
        public async Task Registration_prevented_given_username_containing_non_alphanumerics(string username3, string pin3)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username3;
            manager.Password = pin3;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }

        [Theory]
        [InlineData("LowBobas4896", "dscbhbaerbyuifb896Q32GQBHB")]
        [InlineData("BaltasL_445", "dcbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("AsciiNinja12345", "dscbhaerbyuifb896Q32GQBWHB")]
        [InlineData("Herkus_Leon", "dscbhbaebyuifb896Q32GQBWHB")]
        [InlineData("SSS", "dscbhbaerbyuifb896Q32GBWHB")]
        public async Task Registration_allowed_given_compliant_username(string username4, string pin4)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username4;
            manager.Password = pin4;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Ok", status);

        }
        [Theory]
        [InlineData("BaltasLapasss14", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("GerasVardas78", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("DidelisT87omas", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Popierius14", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("low_bobas_14", "dscbhbaerbyuifb896Q32GQBWHB")]
        public async Task Registration_prevented_given_taken_username(string username5, string pin5)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username5;
            manager.Password = pin5;

            string status = await manager.CheckUsernameStatusAsync();

            if (status != "Taken")
            {
                await manager.RegisterToServerAsync();
            }
            status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Taken", status);

           
        }
        [Theory]
        [InlineData("Baltas_Lapas_14", "")]
        [InlineData("Geras_Katinas_13", "")]
        [InlineData("neilas_labanauskas_8000", "")]
        [InlineData("Astuoni_Keturi", "")]
        [InlineData("Vienas_Du_3", "")]
        public async Task Registration_prevented_given_blank_password(string username6, string pin6)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username6;
            manager.Password = pin6;

            await Assert.ThrowsAsync<RegistrationFailedException>(manager.RegisterToServerAsync);


        }
        [Theory]
        [InlineData("Juodas_Lapas_14", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Blogas_Katinas_13", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("geilas_labanauskas_8000", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Devyni_Keturi", "dscbhbaerbyuifb896Q32GQBWHB")]
        [InlineData("Keturi_Du_3", "dscbhbaerbyuifb896Q32GQBWHB")]
        public async Task Login_prevented_given_user_is_not_registered(string username7, string pin7)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username7;
            manager.Password = pin7;

            await Assert.ThrowsAsync<LoginFailureException>(manager.LoginToServerAsync);
        }
        [Theory]
        [InlineData("tempuserTemp1", "dscbhbaerbyuifb896Q32GQBWHB", "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzFwcm9qZWN0LWZsYXJlLm5ldDB2VGdBOUg0VkF1Z3BoSkNEc3BwR1E$So7RtLAXUnILkxr0rAIUzRCoCYF0QHp5pYoZfzSxaI0")]
        [InlineData("tempuserTemp2", "dscbhbaerbyuifb896Q32GQBWHB", "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzBwcm9qZWN0LWZsYXJlLm5ldGVNVEhJaWl0NlNTcWZKdWg2UEovM3c$tHhA3AmlEH8ao3vypVV36eyzbKfuX2b5a+5OCdD0kJI")]
        [InlineData("tempuserTemp3", "dscbhbaerbyuifb896Q32GQBWHB", "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzJwcm9qZWN0LWZsYXJlLm5ldGljWnJ2R2VlMzBjYU9UL3dCbmlaOHc$L8v9YhMdDXraDo+PA2rTLlY9wYpVs6wYK13qf+SANjM")]
        [InlineData("tempuserTemp4", "dscbhbaerbyuifb896Q32GQBWHB", "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzNwcm9qZWN0LWZsYXJlLm5ldDVBcUt0QWtIRy9MVDczM3FmaENFc3c$2Oc8+v7MK7iXdCEqUpaB4zzq1FGCPC/r4rUC1zVNBb0")]
        [InlineData("tempuserTemp5", "dscbhbaerbyuifb896Q32GQBWHB", "$argon2i$v=19$m=524288,t=3,p=4$dGVzdGluZ191c2VyXzRwcm9qZWN0LWZsYXJlLm5ldEZhMzFvcy9uWWJhQTVsOEQ1RHpvcmc$I7BB3hDxu0m08EUpAFaGwenwyx+IvnVyuLWfJm0XlSg")]
        public async Task Login_successful_given_user_is_registered(string username8, string pin8, string hash)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username8;
            manager.Password = pin8;
            manager.Credentials.Argon2Hash = hash;          

            string status = await manager.CheckUsernameStatusAsync();

            if (status != "Taken")
            {
                await manager.RegisterToServerAsync();
            }
            try
            {
                await manager.LoginToServerAsync();
            }
            catch (LoginFailureException ex)
            {
                Assert.Fail(ex.Message);
            }
            finally
            {
                await manager.RemoveUserFromServerAsync(false);
            }
           
        }



    }
}