using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

using System;
using System.Collections.Generic;

namespace UGUICustom
{
	/// <summary>
	/// 縦方向か横方向フリックへの可動範囲内での移動と、検知の可否を決める機能クラス.
	/// フリックするか、移動しきって手を離した際に、コールバックを飛ばします.
	/// </summary>
	public class UIFlick : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
	{
		//縦フリックによる上方向、横フリックによる右方向の、ポジティブ方向へのコールバック.
		public event Action<GameObject> onPositiveFlicked = delegate {};
		//縦フリックによる下方向、横フリックによる左方向の、ネガティブ方向へのコールバック.
		public event Action<GameObject> onNegativeFlicked = delegate {};

		[Tooltip("縦か横を指定します")]
		[SerializeField] private _Direction _direction     = _Direction.Vertical;
		[Tooltip("可動範囲を設定します")]
		[SerializeField] private float      _movableRange  = 50f;
		[Tooltip("数値が少ないほど動きに抵抗がうまれます")]
		[Range(1, 25)]
		[SerializeField] private float      _stickyFeeling = 12f;

		private RectTransform _rectTransform;
		private Vector2       _defaultPosition      = new Vector2();
		private Vector2       _pointerStartPosition = new Vector2();

		private bool  _isFlick          = false;
		private bool  _isDragging       = false;
		private bool  _isFingerReleased = true;
		private bool  _isDifferentWay   = false;
		private float _draggingTime     = 0f;

		//この時間以内に手を離したらフリックの可能性がある秒.
		private const float _FLICK_THRESHOLD_TIME            = 0.2f;
		//_FLICK_THRESHOLD_TIMERの時間以内にこの距離だけ移動していたら、フリックと決める距離.
		private const float _FLICK_DETECTION_AREA            = 20f;
		//フリックじゃなくても、最大可動範囲から前後以下の値を引いたエリアの外側にいる状態でリリースしたら決定とする距離.
		private const float _MOVABLE_DETECTION_OUTER_AREA    = 5f;
		//縦方向の際の横すぎるドラッグ、横方向の際の縦すぎるドラッグのキャンセル許容範囲.
		private const float _DIFFERENT_WAY_DRAG_CANCEL_RANGE = 120f;

		private enum _Direction
		{
			Vertical,
			Horizontal,
		}

		public RectTransform CachedRectTransform
		{
			get
			{
				if (_rectTransform == null)
				{
					_rectTransform = transform as RectTransform;
				}

				return _rectTransform;
			}
		}

		public void OnBeginDrag(PointerEventData eventData)
		{
			_defaultPosition = CachedRectTransform.anchoredPosition;
			_pointerStartPosition = eventData.position;
			_draggingTime = 0;
			_isDragging   = true;
			_isDifferentWay = false;
		}

		public void OnEndDrag(PointerEventData eventData)
		{
			float startPosition = (_direction == _Direction.Vertical) ? _defaultPosition.y : _defaultPosition.x;
			float movedPosition = (_direction == _Direction.Vertical) ? CachedRectTransform.anchoredPosition.y : CachedRectTransform.anchoredPosition.x;

			CachedRectTransform.anchoredPosition = _defaultPosition;

			_isFingerReleased = true;

			_isFlick    = false;
			_isDragging = false;

			if (_draggingTime <= _FLICK_THRESHOLD_TIME & !_isDifferentWay)
			{
				if (Math.Abs(startPosition - movedPosition) > _FLICK_DETECTION_AREA)
				{
					_isFlick = true;
				}
			}

			if ((_isFlick || Math.Abs(startPosition - movedPosition) > _movableRange - _MOVABLE_DETECTION_OUTER_AREA) & !_isDifferentWay)
			{
				if (startPosition < movedPosition)
				{
					onPositiveFlicked(this.gameObject);
				}
				else if (startPosition > movedPosition)
				{
					onNegativeFlicked(this.gameObject);
				}
			}

			_isDifferentWay = false;
		}

		public void OnDrag(PointerEventData eventData)
		{
			Vector2 pointerPosition = eventData.position;

			if (_direction == _Direction.Horizontal)
			{
				// 縦方向にそれ過ぎたら、ドラッグをキャンセルします.
				if (Mathf.Abs(_pointerStartPosition.y - pointerPosition.y) > _DIFFERENT_WAY_DRAG_CANCEL_RANGE)
				{
					// Lerpで滑らかにデフォルトポジションへ戻します.
					CachedRectTransform.anchoredPosition = Vector2.Lerp(CachedRectTransform.anchoredPosition, _defaultPosition, _draggingTime * _stickyFeeling);
					_isDifferentWay = true;
				}

				if (!_isDifferentWay)
				{
					// Lerpで滑らかにポインターポジションへ移動します.
					float horizontalDistance = Mathf.Clamp(pointerPosition.x - _pointerStartPosition.x, -(_movableRange), _movableRange);
					CachedRectTransform.anchoredPosition = Vector2.Lerp(_defaultPosition, new Vector2(horizontalDistance + _defaultPosition.x, _defaultPosition.y), _draggingTime * _stickyFeeling);
				}
			}
			else if (_direction == _Direction.Vertical)
			{
				// 横方向にそれ過ぎたら、ドラッグをキャンセルします.
				if (Mathf.Abs(_pointerStartPosition.x - pointerPosition.x) > _DIFFERENT_WAY_DRAG_CANCEL_RANGE)
				{
					// Lerpで滑らかにデフォルトポジションへ戻します.
					CachedRectTransform.anchoredPosition = Vector2.Lerp(CachedRectTransform.anchoredPosition, _defaultPosition, _draggingTime * _stickyFeeling);
					_isDifferentWay = true;
				}

				if (!_isDifferentWay)
				{
					// Lerpで滑らかにポインターポジションへ移動します.
					float verticalDistance = Mathf.Clamp(pointerPosition.y - _pointerStartPosition.y, -(_movableRange), _movableRange);
					CachedRectTransform.anchoredPosition = Vector2.Lerp(_defaultPosition, new Vector2(_defaultPosition.x, verticalDistance + _defaultPosition.y), _draggingTime * _stickyFeeling);
				}
			}

			// OnBeginDrag → OnDrag → OnEndDrag となった後に、もしOnDragで再開してしまったら、一度OnBeginDragを発火させておきます.
			// 端末の外側にドラッグしてしまった際や、一瞬指が離れた場合等にOnEndDragのみが走ってしまった場合の対策です.
			if (_isFingerReleased)
			{
				OnBeginDrag(eventData);
				_isFingerReleased = false;
			}
		}

		void Update()
		{
			if (_isDragging)
			{
				_draggingTime += Time.deltaTime;
			}
		}
	}
}
