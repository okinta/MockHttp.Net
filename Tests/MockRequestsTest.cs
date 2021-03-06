﻿using FluentAssertions;
using MockHttp.Net.Exceptions;
using MockHttp.Net;
using RestSharp;
using System;
using Xunit.Sdk;
using Xunit;

namespace Tests
{
    public class MockRequestsTest
    {
        [Fact]
        public void TestAssertAllCalledOnce()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/custom/endpoint", "sample response"));
            var client = new RestClient(requests.Url);
            var request = new RestRequest("custom/endpoint");
            Assert.Equal("sample response", client.Get(request).Content);
            requests.AssertAllCalledOnce();
        }

        [Fact]
        public void TestCalledTwice()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/custom/endpoint", "sample response"));
            var client = new RestClient(requests.Url);
            var request = new RestRequest("custom/endpoint");
            Assert.Equal("sample response", client.Get(request).Content);
            Assert.Equal("sample response", client.Get(request).Content);

            Assert.Throws<RequestCalledTooOftenException>(() =>
                requests.AssertAllCalledOnce());
            Assert.Equal(2, requests[0].Called);
        }

        [Fact]
        public void TestEmptyResponse()
        {
            using var requests = new MockRequests(new HttpHandler("/"));
            var client = new RestClient(requests.Url);
            var request = new RestRequest("");
            Assert.Equal("", client.Get(request).Content);
            requests.AssertAllCalledOnce();
        }

        [Fact]
        public void TestRetryWhenPortInUse()
        {
            var handler = new HttpHandler("/");
            var random = new PredictableNumberGenerator(1, 1, 1, 2, 3, 3, 4);

            using var requests1 = new MockRequests(random.Next, handler);
            Assert.Equal("http://localhost:7101/", requests1.Url);
            var client = new RestClient(requests1.Url);
            var request = new RestRequest("");
            Assert.Equal("", client.Get(request).Content);

            using var requests2 = new MockRequests(random.Next, handler);
            Assert.Equal("http://localhost:7102/", requests2.Url);
            client = new RestClient(requests2.Url);
            Assert.Equal("", client.Get(request).Content);

            using var requests3 = new MockRequests(random.Next, handler);
            Assert.Equal("http://localhost:7103/", requests3.Url);
            client = new RestClient(requests3.Url);
            Assert.Equal("", client.Get(request).Content);

            using var requests4 = new MockRequests(random.Next, handler);
            Assert.Equal("http://localhost:7104/", requests4.Url);
            client = new RestClient(requests4.Url);
            Assert.Equal("", client.Get(request).Content);
        }

        [Fact]
        public void TestAssertNoHandlerExceptions()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/send", "data=54", "we got 54"));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            client.Post(request).Content
                .Should().StartWith(
                    "Exception in handler: Expected content to be equivalent to");
            requests
                .Invoking(r => r.AssertNoHandlerExceptions())
                .Should().Throw<XunitException>();
        }

        [Fact]
        public void TestAssertPostContentError()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/send", "data=54", "we got 54"));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            client.Post(request).Content
                .Should().StartWith(
                    "Exception in handler: Expected content to be equivalent to");
            requests
                .Invoking(r => r.AssertAllCalledOnce())
                .Should().Throw<XunitException>();
        }

        [Fact]
        public void TestAssertPostContent()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/send", "data=54", "we got 54"));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            request.AddParameter("data", 54);
            Assert.Equal("we got 54", client.Post(request).Content);
            requests.AssertAllCalledOnce();
        }

        [Fact]
        public void TestValidateSequenceOfParameters()
        {
            using var requests = new MockRequests(
                new HttpHandler("/send",
                    new ValidateRequestHandler(
                        "data=54", "we got 54"),
                    new ValidateRequestHandler(
                        "data=56", "we got 56")));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            request.AddParameter("data", 54);
            Assert.Equal("we got 54", client.Post(request).Content);

            request = new RestRequest("send");
            request.AddParameter("data", 56);
            Assert.Equal("we got 56", client.Post(request).Content);
            requests.AssertAllCalledOnce();
            Assert.Equal(2, requests.Handlers[0].Called);
        }

        [Fact]
        public void TestValidateIncorrectSequenceOfParameters()
        {
            using var requests = new MockRequests(
                new HttpHandler("/send",
                    new ValidateRequestHandler(
                        "data=54", "we got 54"),
                    new ValidateRequestHandler(
                        "data=56", "we got 56")));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            request.AddParameter("data", 54);

            client.Post(request).Content
                .Should().BeEquivalentTo("we got 54");
            client.Post(request).Content
                .Should().StartWith(
                    "Exception in handler: Expected content to be equivalent to");
            requests
                .Invoking(r => r.AssertAllCalledOnce())
                .Should().Throw<XunitException>();

            requests.Handlers[0].Called
                .Should().Be(2);
        }

        [Fact]
        public void TestTooManySequenceCalls()
        {
            using var requests = new MockRequests(
                new HttpHandler("/send",
                    new ValidateRequestHandler(
                        "data=54", "we got 54"),
                    new ValidateRequestHandler(
                        "data=56", "we got 56")));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            request.AddParameter("data", 54);
            Assert.Equal("we got 54", client.Post(request).Content);

            request = new RestRequest("send");
            request.AddParameter("data", 56);
            Assert.Equal("we got 56", client.Post(request).Content);

            Assert.StartsWith(
                "Exception in handler: No more handlers are available",
                client.Post(request).Content);

            Assert.Throws<InvalidOperationException>(
                () => requests.AssertNoHandlerExceptions());
            Assert.Throws<RequestCalledTooOftenException>(
                () => requests.AssertAllCalledOnce());
            Assert.Equal(3, requests.Handlers[0].Called);
        }

        [Fact]
        public void TestTooFewSequenceCalls()
        {
            using var requests = new MockRequests(
                new HttpHandler("/send",
                    new ValidateRequestHandler(
                        "data=54", "we got 54"),
                    new ValidateRequestHandler(
                        "data=56", "we got 56")));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("send");
            request.AddParameter("data", 54);
            Assert.Equal("we got 54", client.Post(request).Content);

            Assert.Throws<RequestCalledTooFewException>(
                () => requests.AssertAllCalledOnce());
            Assert.Equal(1, requests.Handlers[0].Called);
        }

        [Fact]
        public void TestMultipleResponses()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/get", new[] {"hello 1", "hello 2"}));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("get");
            client.Get(request).Content
                .Should().Be("hello 1");
            client.Get(request).Content
                .Should().Be("hello 2");
            requests.AssertAllCalledOnce();
        }

        [Fact]
        public void TestMultipleResponsesTooFew()
        {
            using var requests = new MockRequests(
                new HttpHandler(
                    "/get", "hello 1", "hello 2", "hello 3"));

            var client = new RestClient(requests.Url);
            var request = new RestRequest("get");
            client.Get(request).Content
                .Should().Be("hello 1");
            client.Get(request).Content
                .Should().Be("hello 2");

            requests
                .Invoking(r => r.AssertAllCalledOnce())
                .Should().Throw<RequestCalledTooFewException>();
        }
    }
}
