#define MULTIKEY_USE_NEWTONSOFT_JSON
using MultiKey;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TestProject;

public sealed record TestItem(Guid Primary, long Secondary, string AdditionalProperty);

[MultiKeyDictionary<Guid, long, TestItem>("item => item.Primary", "item => item.Secondary")]
public partial record TestMultiDict;

[TestFixture]
public class TestMultiDictTests
{
    [Test]
    public void TestEmptyDictionary()
    {
        var dictionary = new TestMultiDict();
        Assert.That(dictionary.Count, Is.EqualTo(0));
    }

    [Test]
    public void TestAddItem()
    {
        var item = new TestItem(Guid.NewGuid(), 1, "Test");
        var dictionary = new TestMultiDict().Add(item);

        Assert.That(dictionary.Count, Is.EqualTo(1));
        Assert.That(dictionary[item.Primary], Is.EqualTo(item));
    }

    [Test]
    public void TestRemoveItem()
    {
        var item = new TestItem(Guid.NewGuid(), 1, "Test");
        var dictionary = new TestMultiDict().Add(item);
        dictionary = dictionary.Remove(item.Primary);

        Assert.That(dictionary.Count, Is.EqualTo(0));
        Assert.That(dictionary.ContainsKey(item.Primary), Is.False);
    }

    [Test]
    public void TestSetItem()
    {
        var primary = Guid.NewGuid();
        var item1 = new TestItem(primary, 1, "Test1");
        var item2 = new TestItem(primary, 1, "Test2");

        var dictionary = new TestMultiDict().Add(item1);
        dictionary = dictionary.SetItem(item2);

        Assert.That(dictionary.Count, Is.EqualTo(1));
        Assert.That(dictionary[primary], Is.EqualTo(item2));
    }

    [Test]
    public void TestGetBySecondaryKey()
    {
        var secondary = 1;
        var item1 = new TestItem(Guid.NewGuid(), secondary, "Test1");
        var item2 = new TestItem(Guid.NewGuid(), secondary, "Test2");

        var dictionary = new TestMultiDict(new List<TestItem> { item1, item2 });

        var items = dictionary[secondary];
        Assert.That(items.Count, Is.EqualTo(2));
        Assert.That(items.Contains(item1));
        Assert.That(items.Contains(item2));
    }

    [Test]
    public void TestJsonSerialization()
    {
        var item = new TestItem(Guid.NewGuid(), 1, "Test");
        var dictionary = new TestMultiDict().Add(item);

        var json = System.Text.Json.JsonSerializer.Serialize(dictionary);
        var deserializedDictionary = System.Text.Json.JsonSerializer.Deserialize<TestMultiDict>(json);

        Assert.That(deserializedDictionary, Is.Not.Null);
        Assert.That(deserializedDictionary.Count, Is.EqualTo(1));
        Assert.That(deserializedDictionary[item.Primary], Is.EqualTo(item));
    }
    
    [Test]
    public void TestNewtonsoftJsonSerialization()
    {
        var item = new TestItem(Guid.NewGuid(), 1, "Test");
        var dictionary = new TestMultiDict().Add(item);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(dictionary);
        var deserializedDictionary = Newtonsoft.Json.JsonConvert.DeserializeObject<TestMultiDict>(json);

        Assert.That(deserializedDictionary, Is.Not.Null);
        Assert.That(deserializedDictionary.Count, Is.EqualTo(1));
        Assert.That(deserializedDictionary[item.Primary], Is.EqualTo(item));
    }
}