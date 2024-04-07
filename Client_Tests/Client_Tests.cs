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
        [InlineData("llllllllllllllllllllllllllllllllllllllllllllllllllllllllllÆ78", "65481234")]
        [InlineData("HobGoblinasssssssssssssssssssssssssssssssssssssss", "67481234")]
        [InlineData("LowBobasssssssssssssssssssssssssssssssssssssssssssssssss", "62481234")]
        [InlineData("HobGoblsasasgasasinasssssssssssssssssssssssssssssssssssssss", "65488234")]
        [InlineData("Kreveteeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", "65481934")]
        public async void Registration_failed_given_username_with_out_of_bounds_length(string username1, string pin1)
        {      

            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username1; 
            manager.PIN = pin1;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }
        [Theory]
        [InlineData("Hob‹oblinasß45", "65481234")]
        [InlineData("Gobl£ingo‘nas42£", "67481234")]
        [InlineData("Ho’bGoblinas42©", "62481234")]
        [InlineData("LowBob‘as42©", "65488234")]
        [InlineData("Low£Bobas42©", "65481934")]
        public async Task Registration_prevented_given_username_containing_non_ASCII_characters(string username2, string pin2)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username2;
            manager.PIN = pin2;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }

        [Theory]
        [InlineData("GerasSlaptazodis!", "65481234")]
        [InlineData("Blogas$Slaptazodis#", "67481234")]
        [InlineData("#$!'[]", "62481234")]
        [InlineData("Yep()keturi*", "65488234")]
        [InlineData("NeVienas(,)", "65481934")]
        public async Task Registration_prevented_given_username_containing_non_alphanumerics(string username3, string pin3)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username3;
            manager.PIN = pin3;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Bad", status);
        }

        [Theory]
        [InlineData("LowBobas4546", "65481234")]
        [InlineData("BaltasL_4", "65481934")]
        [InlineData("AsciiNinja123", "65411234")]
        [InlineData("User1234ASCII_", "65481277")]
        [InlineData("S", "67881234")]
        public async Task Registration_allowed_given_compliant_username(string username4, string pin4)
        {
            ClientManager manager = new ClientManager("https://rpc.f2.project-flare.net");

            manager.Username = username4;
            manager.PIN = pin4;

            string status = await manager.CheckUsernameStatusAsync();

            Assert.Equal("Ok", status);
        }
        
    }
}