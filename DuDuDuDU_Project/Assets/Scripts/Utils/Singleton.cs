// -------------------------------------------------------------------------------------------------
// Filename: Singleton.cs
// Author: Song Ji Hun. [aka. CraZy GolMae] <jihun.song@pocatcom.com>
// Date: 2015.04.23
//
// Desc:
//
// Copyright (c) 2015 Pocatcom. All rights reserved.
// -------------------------------------------------------------------------------------------------
using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace OJ.Utils
{
	/// <summary>
	/// 인스펙터에 채워 둔 데이터가 있어서 <b>빈 객체를 만들면 안 되는</b> 싱글톤에 붙인다.
	///
	/// 이게 없으면 MonoSingleton 은 씬에서 못 찾았을 때 빈 GameObject 를 만든다.
	/// 순수 런타임 서비스에는 맞지만, StaticResource 처럼 인스펙터 참조가 정본인
	/// 타입에는 재앙이다 — 필드가 전부 null 인 인스턴스가 조용히 생기고, 그 뒤로
	/// 모든 조회가 코드 기본값으로 흘러 <b>배선 사고가 "기본값 게임"으로 흡수된다.</b>
	///
	/// 경로는 Resources 기준 상대 경로다. "StaticResource" -> Assets/Resources/StaticResource.prefab
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	[Preserve]
	public sealed class SingletonPrefabAttribute : Attribute
	{
		public string ResourcePath { get; }

		public SingletonPrefabAttribute(string resourcePath)
		{
			ResourcePath = resourcePath;
		}
	}

	/// <summary>
	/// 씬에 없으면 <b>빈 GameObject 를 만들어 써도 되는</b> 싱글톤에 붙인다.
	///
	/// 인스펙터에 채워 둘 값이 없는 순수 런타임 서비스에만 해당한다. 직렬화 필드가
	/// 하나라도 생기는 순간 이 어트리뷰트는 틀린 선택이 된다 — 그 값이 빈 채로
	/// 만들어지기 때문이다. 그때는 [SingletonPrefab] 으로 옮겨야 한다.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	[Preserve]
	public sealed class SingletonAutoCreateAttribute : Attribute
	{
	}

	public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
	{
		static T _instance = null;

		// 제네릭 타입의 static 필드는 닫힌 타입마다 따로 생기므로 T 별로 캐시된다.
		static bool _prefabPathResolved = false;
		static string _prefabPath = null;
		static bool _autoCreateResolved = false;
		static bool _autoCreate = false;
		static bool _creationFailureLogged = false;
		static bool _editModeRequestLogged = false;

		public static T Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = GameObject.FindObjectOfType(typeof(T)) as T;
					if (_instance == null)
					{
						_instance = Create();
					}
					else
					{
						Debug.LogFormat("Find Singleton-instance. Type: {0}, InstanceID: {1}", typeof(T).FullName, _instance.GetInstanceID());
						_instance.Init();
					}
				}
				return _instance;
			}
		}

		/// <summary>
		/// 씬에서 못 찾았을 때 어떻게 할지는 <b>타입이 선언한다.</b>
		///
		///   [SingletonPrefab(경로)]  Resources 의 그 프리팹에서 만든다
		///   [SingletonAutoCreate]    빈 GameObject 를 만든다 (순수 런타임 서비스)
		///   둘 다 없음               <b>만들지 않는다.</b> 명시적 실패
		///
		/// 예전에는 무조건 빈 객체를 만들었다. 그래서 배치를 빠뜨리거나 씬을 직접
		/// 재생했을 때 값이 전부 빈 인스턴스가 조용히 생기고, 게임은 기본값으로
		/// 굴러가면서 아무것도 알려 주지 않았다. (MIGRATION_BASELINE 2.3)
		/// </summary>
		static T Create()
		{
			string prefabPath = PrefabPath;
			if (prefabPath == null)
			{
				if (!AllowsAutoCreate)
				{
					LogCreationFailureOnce(
						"{0} 인스턴스가 없다. 씬이나 프리팹에 배치돼 있어야 한다. 예전에는 여기서 " +
						"빈 객체를 만들어 넘겼지만, 그러면 인스펙터 값이 전부 빈 채로 게임이 굴러가 " +
						"배선 사고가 드러나지 않는다. 런타임에 만들어도 되는 타입이면 " +
						"[SingletonAutoCreate] 를, 프리팹에서 만들어야 하면 [SingletonPrefab] 을 붙일 것.",
						typeof(T).FullName);
					return null;
				}

				Debug.LogFormat("Create Singleton-instance. - Begin - Type: {0}", typeof(T).FullName);

				var obj = new GameObject(typeof(T).ToString());
				T created = obj.AddComponent<T>();  // 이 때 Awake() 호출됨

				Debug.LogFormat("Create Singleton-instance. - End - Type: {0}, InstanceID: {1}", typeof(T).FullName, created.GetInstanceID());

				// Problem during the creation, this should not happen
				if (created == null)
				{
					Debug.LogError("Problem during the creation of " + typeof(T).ToString());
				}

				return created;
			}

			if (!Application.isPlaying)
			{
				// 에디터에서 프리팹을 씬에 심어 버리면 1.1 에서 걷어낸 병(에디터가 씬을
				// 조용히 오염시키는 것)이 그대로 돌아온다. 만들지 않고 알린다.
				if (!_editModeRequestLogged)
				{
					_editModeRequestLogged = true;
					Debug.LogErrorFormat(
						"{0} 를 플레이 중이 아닐 때 요청했다. 에디터에서는 프리팹을 씬에 심지 않는다. " +
						"에디터 도구라면 AssetDatabase 로 에셋을 직접 읽을 것.", typeof(T).FullName);
				}

				return null;
			}

			var prefab = Resources.Load<GameObject>(prefabPath);
			if (prefab == null)
			{
				// 여기서 빈 객체로 물러서지 않는다. 그러면 참조가 전부 null 인 채로
				// 게임이 굴러가고, 무엇이 잘못됐는지 영원히 드러나지 않는다.
				LogCreationFailureOnce(
					"{0} 를 만들 수 없다. Resources/{1} 프리팹이 없다. 씬에 인스턴스를 두거나 프리팹을 복구할 것.",
					typeof(T).FullName, prefabPath);
				return null;
			}

			var instance = Instantiate(prefab);   // Awake() 가 여기서 돌며 _instance 를 채운다
			instance.name = typeof(T).Name;

			if (!instance.activeSelf)
			{
				// 비활성으로 저장된 프리팹은 Awake 가 돌지 않아 Init() 도 건너뛴다.
				// 반쯤 초기화된 인스턴스를 조용히 넘기는 것보다 켜 두는 편이 낫다.
				Debug.LogWarningFormat("Resources/{0} 프리팹이 비활성 상태다. 활성화해서 초기화시킨다.", prefabPath);
				instance.SetActive(true);
			}

			T component = instance.GetComponent<T>();
			if (component == null)
			{
				LogCreationFailureOnce(
					"Resources/{1} 프리팹에 {0} 컴포넌트가 없다. 프리팹이 잘못됐다.",
					typeof(T).FullName, prefabPath);
				Destroy(instance);
				return null;
			}

			Debug.LogFormat("Create Singleton-instance from prefab. Type: {0}, Path: {1}, InstanceID: {2}",
				typeof(T).FullName, prefabPath, component.GetInstanceID());
			return component;
		}

		static string PrefabPath
		{
			get
			{
				if (!_prefabPathResolved)
				{
					_prefabPathResolved = true;
					// TODO(8.8): IL2CPP 스트리핑 시 이 어트리뷰트가 남는지 실기 빌드로 확인할 것.
					var attribute = (SingletonPrefabAttribute)Attribute.GetCustomAttribute(
						typeof(T), typeof(SingletonPrefabAttribute));
					_prefabPath = attribute != null ? attribute.ResourcePath : null;
				}

				return _prefabPath;
			}
		}

		static bool AllowsAutoCreate
		{
			get
			{
				if (!_autoCreateResolved)
				{
					_autoCreateResolved = true;
					_autoCreate = Attribute.GetCustomAttribute(
						typeof(T), typeof(SingletonAutoCreateAttribute)) != null;
				}

				return _autoCreate;
			}
		}

		// 실패해도 _instance 는 null 로 남아 접근할 때마다 재시도한다. 로그까지 매번
		// 찍으면 콘솔이 묻혀 정작 원인을 못 읽으므로 한 번만 남긴다.
		static void LogCreationFailureOnce(string format, params object[] args)
		{
			if (_creationFailureLogged)
				return;

			_creationFailureLogged = true;
			Debug.LogErrorFormat(format, args);
		}

		public static bool isAlive { get { return (_instance != null); } }


		void Awake()
		{
			DontDestroyOnLoad(gameObject);

			if (_instance == null)
			{
				_instance = this as T;
				Debug.LogFormat("Awake Singleton-instance. - OK - Type: {0}, InstanceID: {1}", typeof(T).FullName, _instance.GetInstanceID());

				_instance.Init();
			}
			else if (_instance != this)
			{
				Debug.LogFormat("Awake Singleton-instance. - Duplicate - Type: {0}, InstanceID: {1}, This: {2}", typeof(T).FullName, _instance.GetInstanceID(), this.GetInstanceID());
				Destroy(gameObject);
			}
		}

		// This function is called when the instance is used the first time
		// Put all the initializations you need here, as you would do in Awake
		protected virtual void Init()
		{
			/* BLANK */
		}

		protected virtual void Release()
		{
			/* BLANK */
		}

		void OnDestroy()
		{
			if (_instance == this)
			{
				Debug.LogFormat("Destroy : {0}, InstanceID: {1}", typeof(T).FullName, _instance.GetInstanceID());

				_instance.Release();
				_instance = null;
			}
		}
	}

}
