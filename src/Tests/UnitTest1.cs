using MultiKey;

namespace TestProject;

public sealed record TestItem2(Guid Primary, long Secondary);
public sealed record TestItem3(Guid Primary, long Secondary, float Tertiary);

public static partial class Wrapper
{
    [MultiKeyDictionary<Guid, long, TestItem2>(
        "item => item.Primary", 
        "item => item.Secondary")]
    public partial record TestMultiDict2;
    
    [MultiKeyDictionary<Guid, long, float, TestItem3>(
        "item => item.Primary", 
        "item => item.Secondary",
        "item => item.Tertiary")]
    public partial record TestMultiDict3;
}

[TestFixture]
public class TestMultiDictTests
{
    [Test]
    public void TestEmptyDictionary()
    {
        var dictionary = new Wrapper.TestMultiDict2();
        Assert.That(dictionary, Is.Empty);
    }

     [Test]
     public void TestAddItem()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         Assert.That(dictionary, Has.Count.EqualTo(1));
         Assert.That(dictionary[item.Primary], Is.EqualTo(item));
     }

     [Test]
     public void TestRemoveItem()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);
         dictionary = dictionary.Remove(item.Primary);

         Assert.That(dictionary, Is.Empty);
         Assert.That(dictionary.ContainsKey(item.Primary), Is.False);
     }

     [Test]
     public void TestSetItem()
     {
         var primary = Guid.NewGuid();
         var item1 = new TestItem2(primary, 1);
         var item2 = new TestItem2(primary, 1);

         var dictionary = new Wrapper.TestMultiDict2().Add(item1);
         dictionary = dictionary.SetItem(item2);

         Assert.That(dictionary, Has.Count.EqualTo(1));
         Assert.That(dictionary[primary], Is.EqualTo(item2));
     }

     [Test]
     public void TestGetBySecondaryKey()
     {
         const int secondary = 1;
         var item1 = new TestItem2(Guid.NewGuid(), secondary);
         var item2 = new TestItem2(Guid.NewGuid(), secondary);

         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var items = dictionary.Get2(secondary);
         Assert.That(items, Has.Count.EqualTo(2));
         Assert.That(items, Does.Contain(item1));
         Assert.That(items, Does.Contain(item2));
     }

     [Test]
     public void TestJsonSerialization()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         var json = System.Text.Json.JsonSerializer.Serialize(dictionary);
         var deserializedDictionary = System.Text.Json.JsonSerializer.Deserialize<Wrapper.TestMultiDict2>(json);

         Assert.That(deserializedDictionary, Is.Not.Null);
         Assert.That(deserializedDictionary, Has.Count.EqualTo(1));
         Assert.That(deserializedDictionary[item.Primary], Is.EqualTo(item));
     }

     [Test]
     public void TestNewtonsoftJsonSerialization()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         var json = Newtonsoft.Json.JsonConvert.SerializeObject(dictionary);
         var deserializedDictionary = Newtonsoft.Json.JsonConvert.DeserializeObject<Wrapper.TestMultiDict2>(json);

         Assert.That(deserializedDictionary, Is.Not.Null);
         Assert.That(deserializedDictionary, Has.Count.EqualTo(1));
         Assert.That(deserializedDictionary[item.Primary], Is.EqualTo(item));
     }

     [Test]
     public void TestTryGetValuePrimaryKey()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         var result = dictionary.TryGetValue(item.Primary, out var retrievedItem);

         Assert.Multiple(() =>
         {
             Assert.That(result, Is.True);
             Assert.That(retrievedItem, Is.EqualTo(item));
         });
     }

     [Test]
     public void TestTryGetValueSecondaryKey()
     {
         const int secondary = 1;
         var item1 = new TestItem2(Guid.NewGuid(), secondary);
         var item2 = new TestItem2(Guid.NewGuid(), secondary);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var result = dictionary.TryGetValue2(secondary, out var retrievedItems);

         Assert.Multiple(() =>
         {
             Assert.That(result, Is.True);
             Assert.That(retrievedItems, Has.Count.EqualTo(2));
         });
         Assert.That(retrievedItems, Does.Contain(item1));
         Assert.That(retrievedItems, Does.Contain(item2));
     }

     [Test]
     public void TestContainsKeyPrimary()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         var result = dictionary.ContainsKey(item.Primary);

         Assert.That(result, Is.True);
     }

     [Test]
     public void TestContainsKeySecondary()
     {
         const int secondary = 1;
         var item = new TestItem2(Guid.NewGuid(), secondary);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         var result = dictionary.ContainsKey2(secondary);

         Assert.That(result, Is.True);
     }

     [Test]
     public void TestCompact()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2, null }!);

         var compactedDictionary = dictionary.Compact();

         Assert.That(compactedDictionary, Has.Count.EqualTo(2));
         Assert.That(compactedDictionary.Values, Does.Contain(item1));
         Assert.That(compactedDictionary.Values, Does.Contain(item2));
     }

     [Test]
     public void TestRemoveByItem()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2().Add(item);

         dictionary = dictionary.Remove(item);

         Assert.That(dictionary, Is.Empty);
         Assert.That(dictionary.ContainsKey(item.Primary), Is.False);
     }

     [Test]
     public void TestAddDuplicatePrimaryKey()
     {
         var primary = Guid.NewGuid();
         var item1 = new TestItem2(primary, 1);
         var item2 = new TestItem2(primary, 2);
         var dictionary = new Wrapper.TestMultiDict2().Add(item1);

         Assert.Throws<ArgumentException>(() => dictionary.Add(item2));
     }

     [Test]
     public void TestIteration()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var items = new List<TestItem2>();
         foreach (KeyValuePair<Guid, TestItem2> kvp in dictionary)
         {
             items.Add(kvp.Value);
         }

         Assert.That(items, Has.Count.EqualTo(2));
         Assert.That(items, Does.Contain(item1));
         Assert.That(items, Does.Contain(item2));
     }

     [Test]
     public void TestRetrieveKeys()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var keys = dictionary.Keys.ToList();

         Assert.That(keys, Has.Count.EqualTo(2));
         Assert.That(keys, Does.Contain(item1.Primary));
         Assert.That(keys, Does.Contain(item2.Primary));
     }

     [Test]
     public void TestRetrieveSecondaryKeys()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var keysSecondary = dictionary.Keys2.ToList();

         Assert.That(keysSecondary, Has.Count.EqualTo(2));
         Assert.That(keysSecondary, Does.Contain(item1.Secondary));
         Assert.That(keysSecondary, Does.Contain(item2.Secondary));
     }

     [Test]
     public void TestRetrieveValues()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2(new[] { item1, item2 });

         var values = dictionary.Values.ToList();

         Assert.That(values, Has.Count.EqualTo(2));
         Assert.That(values, Does.Contain(item1));
         Assert.That(values, Does.Contain(item2));
     }

     [Test]
     public void TestNonExistentPrimaryKey()
     {
         var dictionary = new Wrapper.TestMultiDict2();

         Assert.Throws<KeyNotFoundException>(() =>
         {
             var _ = dictionary[Guid.NewGuid()];
         });
     }

     [Test]
     public void TestNonExistentSecondaryKey()
     {
         var dictionary = new Wrapper.TestMultiDict2();

         var items = dictionary.Get2(999L); // Non-existent secondary key
         Assert.That(items, Is.Empty);
     }

     [Test]
     public void TestAddMultipleItems()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var item3 = new TestItem2(Guid.NewGuid(), 3);
         var dictionary = new Wrapper.TestMultiDict2().Add(item1).Add(item2).Add(item3);

         Assert.That(dictionary, Has.Count.EqualTo(3));
         Assert.That(dictionary[item1.Primary], Is.EqualTo(item1));
         Assert.That(dictionary[item2.Primary], Is.EqualTo(item2));
         Assert.That(dictionary[item3.Primary], Is.EqualTo(item3));
     }

     [Test]
     public void TestToTestMultiDictExtensionMethod()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);

         var items = new List<TestItem2> { item1, item2 };
         var dictionary = items.ToWrapperTestMultiDict2();

         Assert.That(dictionary, Has.Count.EqualTo(2));
         Assert.That(dictionary[item1.Primary], Is.EqualTo(item1));
         Assert.That(dictionary[item2.Primary], Is.EqualTo(item2));
     }

     [Test]
     public void TestRemoveNonExistentItem()
     {
         var item = new TestItem2(Guid.NewGuid(), 1);
         var dictionary = new Wrapper.TestMultiDict2();

         var newDictionary = dictionary.Remove(item.Primary);

         Assert.That(newDictionary, Is.Empty);
     }

     [Test]
     public void TestRemoveAllItems()
     {
         var item1 = new TestItem2(Guid.NewGuid(), 1);
         var item2 = new TestItem2(Guid.NewGuid(), 2);
         var dictionary = new Wrapper.TestMultiDict2().Add(item1).Add(item2);

         dictionary = dictionary.Remove(item1.Primary).Remove(item2.Primary);

         Assert.That(dictionary, Is.Empty);
     }
}