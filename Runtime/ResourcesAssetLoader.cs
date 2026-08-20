using System;
using UnityEngine;
using Object = UnityEngine.Object;
using com.ktgame.assets.loader.core;
using Cysharp.Threading.Tasks;

namespace com.ktgame.assets.loader.resources
{
	public sealed class ResourcesAssetLoader : IAssetLoader
	{
		private int _nextRequestId;

		public AssetRequest<TAsset> Load<TAsset>(string address) where TAsset : Object
		{
			var requestId = _nextRequestId++;

			var request = new AssetRequest<TAsset>(requestId);
			var setter = (IAssetRequest<TAsset>)request;
			var result = Resources.Load<TAsset>(address);

			setter.SetResult(result);

			var status = result != null ? AssetRequestStatus.Succeeded : AssetRequestStatus.Failed;
			setter.SetStatus(status);

			if (result == null)
			{
				var exception = new InvalidOperationException($"Requested asset (Key: {address}) was not found.");
				setter.SetOperationException(exception);
				setter.SetTask(UniTask.FromException<TAsset>(exception));
			}
			else
			{
				setter.SetTask(UniTask.FromResult(result));
			}

			setter.SetProgressFunc(() => 1.0f);
			return request;
		}

		public AssetRequest<Object> Load(string address)
		{
			return Load<Object>(address);
		}

		public AssetRequest<TAsset> LoadAsync<TAsset>(string address) where TAsset : Object
		{
			var requestId = _nextRequestId++;

			var request = new AssetRequest<TAsset>(requestId);
			var setter = (IAssetRequest<TAsset>)request;
			var utcs = new UniTaskCompletionSource<TAsset>();

			var req = Resources.LoadAsync<TAsset>(address);

			req.completed += _ =>
			{
				var result = req.asset as TAsset;
				setter.SetResult(result);

				var status = result != null ? AssetRequestStatus.Succeeded : AssetRequestStatus.Failed;
				setter.SetStatus(status);

				if (result == null)
				{
					var exception = new InvalidOperationException($"Requested asset (Key: {address}) was not found.");
					setter.SetOperationException(exception);
					utcs.TrySetException(exception);
				}
				else
				{
					utcs.TrySetResult(result);
				}
			};

			setter.SetProgressFunc(() => req.progress);
			setter.SetTask(utcs.Task);
			return request;
		}

		public AssetRequest<Object> LoadAsync(string address)
		{
			return LoadAsync<Object>(address);
		}

		public void Release(AssetRequest request)
		{
			if (request == null) return;

			try
			{
				var property = request.GetType().GetProperty("Result");
				if (property != null)
				{
					var asset = property.GetValue(request) as Object;
					
					// Chỉ Unload được các Asset tĩnh (Audio, Texture). Unity cấm Unload GameObject/Component.
					if (asset != null && !(asset is GameObject) && !(asset is Component))
					{
						Resources.UnloadAsset(asset);
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning($"[ResourcesAssetLoader] Failed to release asset: {e.Message}");
			}
		}
	}
}
