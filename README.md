# XSerializer
A customizable XML serializer based on XML to LINQ.

## Features
* Serialization
	* Public property/field serialization. (private ones will be supported later)
		* Including built-in simple types & its `Nullable` from.
		* Supports property/field that uses another class as type.
		* Supports collections that implements `IEnumerable`.
	* Serialized collecions can have attributes.
	* Complex classes can be converted to / back from string and stored in attributes. (By implementing `IXStringSerializable`)
	* Attribute-controlled XML element/field generation.
		* Opt-in member generation mode (That is, only attributed members will be serialized.)
		* Supports "AnyElement" & "AnyAttribute".
		* Supports custom XML name & namespace (for type / member / collecion items).
	* ~~Multi-referenced instance serialization. ~~ (See <u>Multiple & Circular References</u>)
	* Serialization callbacks. (Not Implemented Yet)
* Deserialization (Uh oh… Not Implemented Yet)

## Background
I just want to implement the functionality of `System.Xml.Serialization.XmlSerializer` on my own. Maybe you've noticed that `XmlSerializer` enables us to create & load XML documents from & to classes, and the behaviors can be adjusted via different Attributes. Compared with DataContractSeriallizer, it can give you more control on what the XML document should look like. However, the mechanism of `XmlSerializer` is still rigid in the way that you can't gain full control on some elements / attributes, while maintain the rest of the members clean & tidy. For example, a commonly occurred problem to who use `XmlSerializer` is that, it cannot properly handle multiple references towards the same instance. Instead, the same instance is stored multiple times in XML. Hence, it also cannot handle circular references.

I've searched about it for some time, and found that the reference problem can be solved when using `DataContractSeriallizer`. However, it stores all the members as XML elements, and this behavior cannot be tweaked.

This is not the only thing that afflicts me. In fact, I thought about bypassing this reference problem by using custom callback functions, but unfortunately, for some reason, `XmlSerializer` does not implement IFormatter, maybe for which neither does it conform to the conventions of serialization formatters like OnSerializingAttribute, OnDeserializingAttribute, etc. This means I cannot adjust members during the serialization / deserialization procedure.

Sure, you can use `IXmlSerializable` to gain full control over the serialization, at the cost of implementing all the details, including those members that can (and *should*) be taken care of by the Serializer, all by yourself.

Also `XmlSerializer` cannot provide extra information during the serialization process, which has been achieved in `IFormatter` series, I mean, `StreamingContext`.

So… I'm going to implement a serializer on my own.

## Some Details
Now I only expect to implement these features ASAP, so I use XML to LINQ, which simplifies the process of XML.

I also use reflection to access members, which in future, may be switched to some more efficient ways, like dynamically-generated assemblies or something.

### Multiple & Circular References
I've considered this lately, and now reached to the idea that, the classes that is to be used as DOM representation of XML **should not** contain such references. I mean, these classes is only used as a proxy for us to **access XML elements & attributes** more easily, and it's obvious that such references cannot be represented directly in XML element tree. Instead, we should use something like Primary Key and Foreign Key to connect the objects together, and I suggest that these keys be declared explicitly in your DOM classes.

As for `DataContractSeriallizer`, the main purpose of this class is to transfer an object across certain boundaries like application ones and network ones, in which case, what the XML look like doesn't really matter, and certainly, `DataContractSeriallizer` will take care of circular references with its own representation (`z:Id` and `z:Ref`).

For those who intend to connect two classes together by direct reference, I want to say, if these two classes conform a parent-child relation, then go on and put direct references. If not, and is usually when painful circular references appear, I suggest you break up these references into PK and FK. You should take care of the connection later in a higher architectural level (e.g. add a helper function in View Model, which is used to get the instance via specific Key).

So, in current and later versions, just like `XmlSerializer`, **circular references are not supported** and would cause exceptions. Still, you can transform and rebuild such reference it by using serialization callbacks (which hasn't been implemented yet) and user-defined serialization context.

----------

Then, what else can XSerializer do? At this point, I mean, merely 2 days after I started this repos, I'm not pretty sure. But it now supports `Nullable<T>` and can handle it just like other simple types, and you can define your own rules to convert between class and string that used to store in attributes (or elements), and collections can have attributes. These features are *not* included in `System.Xml.Serialization.XmlSerializer`.

In later versions, maybe it'll supports private members' serialization, just like `DataContractSeriallizer`, while giving you more control on what the output should look like.

Maybe I'm reinventing the wheel? Maybe. Yet feel so good.

On the way!


CXuesong *a.k.a. forest93*

2015-07