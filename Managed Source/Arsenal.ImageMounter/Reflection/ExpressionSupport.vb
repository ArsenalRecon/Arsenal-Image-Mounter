Imports System.Collections.ObjectModel
Imports System.Data
Imports System.Diagnostics.CodeAnalysis
Imports System.Linq.Expressions
Imports System.Reflection
Imports System.Runtime.InteropServices
Imports System.Xml.Serialization

Namespace Reflection

    <ComVisible(False)>
    Public MustInherit Class ExpressionSupport

        Private Sub New()

        End Sub

        Private Class ParameterCompatibilityComparer
            Implements IEqualityComparer(Of Type)

            Private Sub New()
            End Sub

            Private Shared ReadOnly Instance As New ParameterCompatibilityComparer

            Private Overloads Function Equals(x As Type, y As Type) As Boolean Implements IEqualityComparer(Of Type).Equals
                Return x.IsAssignableFrom(y)
            End Function

            Private Overloads Function GetHashCode(obj As Type) As Integer Implements IEqualityComparer(Of Type).GetHashCode
                Return obj.GetHashCode()
            End Function

            Public Shared Function Compatible(dest As MethodInfo, sourceReturnType As Type, sourceParameters As Type()) As Boolean
                Return _
                    dest.ReturnType.IsAssignableFrom(sourceReturnType) AndAlso
                    dest.GetParameters().Select(Function(dparam) dparam.ParameterType).SequenceEqual(sourceParameters, Instance)
            End Function
        End Class

        Public Shared Function CreateLocalFallbackFunction(MethodName As String,
                                                           GenericArguments As Type(),
                                                           MethodArguments As IEnumerable(Of Expression),
                                                           ReturnType As Type,
                                                           InvertResult As Boolean,
                                                           RuntimeMethodDetection As Boolean) As [Delegate]

            Dim staticArgs =
                Aggregate arg In MethodArguments
                Select If(arg.NodeType = ExpressionType.Quote, DirectCast(arg, UnaryExpression).Operand, arg)
                Into ToArray()

            Dim staticArgsTypes = Array.ConvertAll(staticArgs, Function(arg) arg.Type)

            Dim newMethod = GetCompatibleMethod(GetType(Enumerable), True, MethodName, GenericArguments, ReturnType, staticArgsTypes)

            If newMethod Is Nothing Then
                Throw New NotSupportedException("Expression calls unsupported method " & MethodName & ".")
            End If

            '' Substitute first argument (extension method source object) with a parameter that
            '' will be substituted with the result sequence when resulting lambda conversion
            '' routine is called locally after data has been fetched from external data service.
            Dim sourceObject = Expression.Parameter(newMethod.GetParameters()(0).ParameterType, "source")

            staticArgs(0) = sourceObject

            Dim newCall As Expression = Expression.Call(newMethod, staticArgs)
            If InvertResult Then
                newCall = Expression.Not(newCall)
            End If
            Dim enumerableStaticDelegate = Expression.Lambda(newCall, sourceObject).Compile()

            If RuntimeMethodDetection Then

                Dim instanceArgs = staticArgs.Skip(1).ToArray()
                Dim instanceArgsTypes = staticArgsTypes.Skip(1).ToArray()

                Dim delegateType = enumerableStaticDelegate.GetType()
                Dim delegateInvokeMethod = delegateType.GetMethod("Invoke")

                Dim getDelegateInstanceOrDefault As Expression(Of Func(Of Object, [Delegate])) =
                    Function(obj) If(GetCompatibleMethodAsDelegate(obj.GetType(),
                                                                   False,
                                                                   InvertResult,
                                                                   MethodName,
                                                                   GenericArguments,
                                                                   ReturnType,
                                                                   instanceArgsTypes,
                                                                   sourceObject,
                                                                   instanceArgs),
                                     enumerableStaticDelegate)

                Dim exprGetDelegateInstanceOrDefault = Expression.Invoke(getDelegateInstanceOrDefault, sourceObject)

                Dim exprCallDelegateInvokeMethod = Expression.Call(Expression.TypeAs(exprGetDelegateInstanceOrDefault, delegateType), delegateInvokeMethod, sourceObject)

                Return Expression.Lambda(exprCallDelegateInvokeMethod, sourceObject).Compile()

            Else

                Return enumerableStaticDelegate

            End If

        End Function

        Public Shared Function GetCompatibleMethodAsDelegate(TypeToSearch As Type,
                                                             FindStaticMethod As Boolean,
                                                             InvertResult As Boolean,
                                                             MethodName As String,
                                                             GenericArguments As Type(),
                                                             ReturnType As Type,
                                                             AlternateArgsTypes As Type(),
                                                             Instance As ParameterExpression,
                                                             Args As Expression()) As [Delegate]

            Dim dynMethod = GetCompatibleMethod(TypeToSearch, FindStaticMethod, MethodName, GenericArguments, ReturnType, AlternateArgsTypes)
            If dynMethod Is Nothing Then
                Return Nothing
            End If
            Dim callExpr As Expression
            If FindStaticMethod Then
                callExpr = Expression.Call(dynMethod, Args)
            Else
                callExpr = Expression.Call(Expression.TypeAs(Instance, dynMethod.DeclaringType), dynMethod, Args)
            End If
            If InvertResult Then
                callExpr = Expression.Not(callExpr)
            End If
            Return Expression.Lambda(callExpr, Instance).Compile()

        End Function

        Public Shared Function GetCompatibleMethod(TypeToSearch As Type,
                                                   FindStaticMethod As Boolean,
                                                   MethodName As String,
                                                   GenericArguments As Type(),
                                                   ReturnType As Type,
                                                   AlternateArgsTypes As Type()) As MethodInfo

            Static methodCache As New Dictionary(Of String, MethodInfo)

            Dim newMethod As MethodInfo = Nothing

            Dim key = String.Concat(ReturnType.ToString(),
                                    ":",
                                    MethodName,
                                    ":",
                                    String.Join(":",
                                                Array.ConvertAll(AlternateArgsTypes, Function(argType) argType.ToString())))

            SyncLock methodCache

                If Not methodCache.TryGetValue(key, newMethod) Then

                    Dim methodNames = {MethodName, "get_" & MethodName}

                    newMethod =
                        Aggregate m In TypeToSearch.GetMethods(BindingFlags.Public Or If(FindStaticMethod, BindingFlags.Static, BindingFlags.Instance))
                        Where
                            methodNames.Contains(m.Name) AndAlso
                            m.GetParameters().Length = AlternateArgsTypes.Length AndAlso
                            m.IsGenericMethodDefinition AndAlso
                            m.GetGenericArguments().Length = GenericArguments.Length
                        Select m = m.MakeGenericMethod(GenericArguments)
                        Into FirstOrDefault(
                            ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes))

                    If newMethod Is Nothing AndAlso Not FindStaticMethod Then
                        For Each interf In From i In TypeToSearch.GetInterfaces()
                                           Where
                                            i.IsGenericType() AndAlso
                                            i.GetGenericArguments().Length = GenericArguments.Length

                            newMethod =
                                Aggregate m In interf.GetMethods(BindingFlags.Public Or BindingFlags.Instance)
                                Into FirstOrDefault(
                                    methodNames.Contains(m.Name) AndAlso
                                    ParameterCompatibilityComparer.Compatible(m, ReturnType, AlternateArgsTypes))

                            If newMethod IsNot Nothing Then
                                Exit For
                            End If

                        Next
                    End If

                    methodCache.Add(key, newMethod)

                End If

            End SyncLock

            Return newMethod

        End Function

        Public Shared Function GetListItemsType(Type As Type) As Type

            While Type.HasElementType
                Type = Type.GetElementType()
            End While

            Static listItemsTypes As New Dictionary(Of Type, Type())

            Dim i As Type() = Nothing

            SyncLock listItemsTypes

                If Not listItemsTypes.TryGetValue(Type, i) Then

                    i =
                        Aggregate ifc In Type.GetInterfaces()
                        Where ifc.IsGenericType AndAlso ifc.GetGenericTypeDefinition() Is GetType(IList(Of ))
                        Select ifc.GetGenericArguments()(0)
                        Into ToArray()

                    listItemsTypes.Add(Type, i)

                End If

            End SyncLock

            If i.Length = 0 Then
                Return Type
            ElseIf i.Length = 1 Then
                Return i(0)
            End If

            Throw New NotSupportedException("More than one element type detected for list type " & Type.ToString() & ".")

        End Function

        Private NotInheritable Class ExpressionMemberEqualityComparer
            Implements IEqualityComparer(Of MemberInfo)

            Public Overloads Function Equals(x As MemberInfo, y As MemberInfo) As Boolean Implements IEqualityComparer(Of MemberInfo).Equals
                If ReferenceEquals(x, y) OrElse
                    (x.DeclaringType Is y.DeclaringType AndAlso
                     x.MetadataToken = y.MetadataToken) Then

                    Return True
                Else
                    Return False
                End If
            End Function

            Public Overloads Function GetHashCode(obj As MemberInfo) As Integer Implements IEqualityComparer(Of System.Reflection.MemberInfo).GetHashCode
                Return obj.DeclaringType.MetadataToken Xor obj.MetadataToken
            End Function
        End Class

        Public Shared ReadOnly Property MemberSequenceEqualityComparer As New SequenceEqualityComparer(Of MemberInfo)(New ExpressionMemberEqualityComparer)

        Public Shared Function GetDataFieldMappings(ElementType As Type) As Dictionary(Of IEnumerable(Of MemberInfo), String)

            Static dataMappings As New Dictionary(Of Type, Dictionary(Of IEnumerable(Of MemberInfo), String))

            Dim mappings As Dictionary(Of IEnumerable(Of MemberInfo), String) = Nothing

            SyncLock dataMappings

                If Not dataMappings.TryGetValue(ElementType, mappings) Then

                    Dim _mappings =
                        From prop In ElementType.GetMembers(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.FlattenHierarchy)
                        Where
                            (prop.MemberType = MemberTypes.Property AndAlso
                                DirectCast(prop, PropertyInfo).GetIndexParameters().Length = 0 AndAlso
                                DirectCast(prop, PropertyInfo).CanRead AndAlso
                                DirectCast(prop, PropertyInfo).CanWrite) OrElse
                            (prop.MemberType = MemberTypes.Field AndAlso
                                Not DirectCast(prop, FieldInfo).IsInitOnly)
                        Select Props = DirectCast({prop}, IEnumerable(Of MemberInfo)), prop.Name

                    mappings = _mappings.ToDictionary(Function(m) m.Props, Function(m) m.Name, _MemberSequenceEqualityComparer)

                    Dim submappings =
                        From props In mappings.Keys.ToArray()
                        Let prop = props(0)
                        Where
                            (prop.MemberType = MemberTypes.Property AndAlso
                                DirectCast(prop, PropertyInfo).GetIndexParameters().Length = 0 AndAlso
                                DirectCast(prop, PropertyInfo).CanRead AndAlso
                                DirectCast(prop, PropertyInfo).CanWrite) OrElse
                            (prop.MemberType = MemberTypes.Field AndAlso
                                Not DirectCast(prop, FieldInfo).IsInitOnly)
                        Let
                            type = GetListItemsType(If(prop.MemberType = MemberTypes.Property,
                                                    DirectCast(prop, PropertyInfo).PropertyType,
                                                    DirectCast(prop, FieldInfo).FieldType))
                        Where
                            (Not type.IsPrimitive) AndAlso
                            type IsNot GetType(String)
                        From submapping In GetDataFieldMappings(type)
                        Select
                            Key = submapping.Key.Concat(props),
                            Value = $"{prop.Name}.{submapping.Value}"

                    For Each submapping In submappings
                        mappings.Add(submapping.Key, submapping.Value)
                    Next

                    dataMappings.Add(ElementType, mappings)

                End If

            End SyncLock

            Return mappings

        End Function

        Public Shared Function GetPropertiesWithAttributes(Of TAttribute As Attribute)(type As Type) As ReadOnlyCollection(Of String)

            Return AttributedMemberFinder(Of TAttribute).GetPropertiesWithAttributes(type)

        End Function

        Private Class AttributedMemberFinder(Of TAttribute As Attribute)

            Public Shared Function GetPropertiesWithAttributes(type As Type) As ReadOnlyCollection(Of String)

                Static cache As New Dictionary(Of Type, ReadOnlyCollection(Of String))

                Dim prop As ReadOnlyCollection(Of String) = Nothing

                SyncLock cache

                    If Not cache.TryGetValue(type, prop) Then
                        prop = Array.AsReadOnly(
                            Aggregate p In type.GetMembers(BindingFlags.Public Or BindingFlags.Instance Or BindingFlags.FlattenHierarchy)
                                Where Attribute.IsDefined(p, GetType(TAttribute))
                                Select p.Name
                                Into ToArray())

                        cache.Add(type, prop)
                    End If

                End SyncLock

                Return prop

            End Function

        End Class

        Public Shared Function GetDataTableName(Of TContext)(entityType As Type) As String

            Return DataContextPropertyFinder(Of TContext).GetDataTableName(entityType)

        End Function

        Private Class DataContextPropertyFinder(Of TContext)

            Public Shared Function GetDataTableName(entityType As Type) As String

                Static properties As New Dictionary(Of Type, String)

                Dim prop As String = Nothing

                SyncLock properties

                    If Not properties.TryGetValue(entityType, prop) Then
                        prop =
                            (Aggregate attr In GetType(TContext).GetProperty("Items").GetCustomAttributes(True).OfType(Of XmlElementAttribute)()
                                Into [Single](attr.Type.IsAssignableFrom(entityType))).ElementName

                        properties.Add(entityType, prop)
                    End If

                End SyncLock

                Return prop

            End Function

        End Class

        Public Shared Function GetLambdaBody(expression As Expression) As Expression

            Return GetLambdaBody(expression, Nothing)

        End Function

        Public Shared Function GetLambdaBody(expression As Expression, <Out> ByRef parameters As ReadOnlyCollection(Of ParameterExpression)) As Expression

            If expression.NodeType <> ExpressionType.Quote Then
                parameters = Nothing
                Return expression
            End If

            Dim expr = DirectCast(DirectCast(expression, UnaryExpression).Operand, LambdaExpression)

            parameters = expr.Parameters

            Return expr.Body

        End Function

        Private Class PropertiesAssigners(Of T)

            Public Shared ReadOnly Property Getters As Dictionary(Of String, Func(Of T, Object))

            Public Shared ReadOnly Property Setters As Dictionary(Of String, Action(Of T, Object))

            Public Shared ReadOnly Property Types As Dictionary(Of String, Type)

            Shared Sub New()

                Dim target = Expression.Parameter(GetType(T), "targetObject")

                Dim props =
                    Aggregate m In GetType(T).GetMembers(BindingFlags.Public Or BindingFlags.Instance)
                    Let p = TryCast(m, PropertyInfo)
                    Let f = TryCast(m, FieldInfo)
                    Where
                        (p IsNot Nothing AndAlso
                         p.CanRead AndAlso
                         p.CanWrite AndAlso
                         p.GetIndexParameters().Length = 0) OrElse
                        (f IsNot Nothing AndAlso
                         Not f.IsInitOnly)
                    Let proptype = If(p IsNot Nothing, p.PropertyType, f.FieldType)
                    Let name = m.Name
                    Let member = Expression.PropertyOrField(target, m.Name)
                        Into ToArray()

                Dim getters =
                    From m In props
                    Select
                        m.name,
                        valueconverted = If(m.proptype.IsValueType,
                            Expression.Convert(m.member, GetType(Object)),
                            Expression.TypeAs(m.member, GetType(Object)))

                _Getters = getters.ToDictionary(Function(m) m.name,
                        Function(m) Expression.Lambda(Of Func(Of T, Object))(
                        m.valueconverted, target).Compile(),
                        StringComparer.OrdinalIgnoreCase)

                Dim setters =
                    From m In props
                    Let value = Expression.Parameter(GetType(Object), "value")
                    Let valueconverted = If(m.proptype.IsValueType,
                        DirectCast(Expression.ConvertChecked(value, m.proptype), Expression),
                        DirectCast(Expression.Condition(
                        Expression.TypeIs(value, m.proptype),
                        Expression.TypeAs(value, m.proptype),
                        Expression.ConvertChecked(value, m.proptype)), Expression))
                    Select
                        m.name,
                        assign = Expression.Assign(m.member, valueconverted),
                        value

                _Setters = setters.ToDictionary(Function(m) m.name,
                                     Function(m) Expression.Lambda(Of Action(Of T, Object))(
                                     m.assign, target, m.value).Compile(),
                                     StringComparer.OrdinalIgnoreCase)

                _Types = props.ToDictionary(Function(m) m.name, Function(m) m.proptype, StringComparer.OrdinalIgnoreCase)

            End Sub

        End Class

        Public Shared Function GetPropertyGetters(Of T As New)() As Dictionary(Of String, Func(Of T, Object))

            Return New Dictionary(Of String, Func(Of T, Object))(PropertiesAssigners(Of T).Getters, PropertiesAssigners(Of T).Getters.Comparer)

        End Function

        Public Shared Function GetPropertySetters(Of T As New)() As Dictionary(Of String, Action(Of T, Object))

            Return New Dictionary(Of String, Action(Of T, Object))(PropertiesAssigners(Of T).Setters, PropertiesAssigners(Of T).Setters.Comparer)

        End Function

        Public Shared Function GetPropertyTypes(Of T As New)() As Dictionary(Of String, Type)

            Return New Dictionary(Of String, Type)(PropertiesAssigners(Of T).Types, PropertiesAssigners(Of T).Types.Comparer)

        End Function

        Public Shared Function RecordToEntityObject(Of T As New)(record As IDataRecord) As T

            Return RecordToEntityObject(record, New T)

        End Function

        Public Shared Function RecordToEntityObject(Of T)(record As IDataRecord, obj As T) As T

            Dim props = PropertiesAssigners(Of T).Setters

            For i = 0 To record.FieldCount - 1
                Dim prop As Action(Of T, Object) = Nothing
                If props.TryGetValue(record.GetName(i), prop) Then
                    prop(obj, If(TypeOf record(i) Is DBNull, Nothing, record(i)))
                End If
            Next

            Return obj

        End Function

    End Class

End Namespace

