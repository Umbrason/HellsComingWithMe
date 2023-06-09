using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpatialOcttree<T> : IReadOnlySpatialOcttree<T>
{
    private SpatialOcttree<T>[] children = new SpatialOcttree<T>[0];
    private Vector3 center;
    private float sideLength;

    const float minSideLength = .5f;

    private struct Entry
    {
        public Vector3 key;
        public T value;
        public Entry(Vector3 key, T value)
        {
            this.key = key;
            this.value = value;
        }
    }
    private readonly List<Entry> items = new();

    public int Count { get; private set; }

    public SpatialOcttree(Vector3 center, float sideLength)
    {
        this.center = center;
        this.sideLength = sideLength;
    }

    public T this[Vector3 key]
    {
        get
        {
            if (children.Length == 0) return items.FirstOrDefault(i => i.key == key).value;
            return children[KeyToIndex(key)][key];
        }
    }

    public void Add(Vector3 key, T item)
    {
        Count++;
        if (Count == 1 || sideLength / 2f < minSideLength)
        {
            items.Add(new(key, item));
            return;
        }
        if (children.Length == 0) Split();
        children[KeyToIndex(key)].Add(key, item);
    }

    private readonly int[] signs = new int[] { -1, 1 };
    void Split()
    {
        children = new SpatialOcttree<T>[8];
        foreach (var x in signs)
            foreach (var y in signs)
                foreach (var z in signs)
                {
                    var center = this.center + new Vector3(x, y, z) / 4f * sideLength;
                    var index = KeyToIndex(center);
                    children[index] = new(center, sideLength / 2f);
                }
        if (Count == 0) return;
        foreach (var item in items)
            children[KeyToIndex(item.key)].Add(item.key, item.value);
        this.items.Clear();
    }

    public bool Remove(Vector3 key)
    {
        if (Count == 0) return false;
        if (children.Length == 0)
        {
            var index = items.FindIndex(i => i.key == key);
            if (index >= 0)
            {
                items.RemoveAt(index);
                Count--;
                return true;
            }
            return false;
        }
        if (!children[KeyToIndex(key)].Remove(key)) return false;
        Count--;
        if (Count == 1)
        {
            var child = children.First(c => c.Count == 1);
            items.AddRange(child.items);
            children = new SpatialOcttree<T>[0];
        }
        return true;
    }

    private int KeyToIndex(Vector3 key)
    {
        var index = (key.x > center.x ? 1 : 0) +
                    (key.y > center.y ? 2 : 0) +
                    (key.z > center.z ? 4 : 0);
        return index;
    }

    public void GetAll(List<T> list)
    {
        for (int i = 0; i < children.Length; i++)
            children[i].GetAll(list);
        for (int i = 0; i < items.Count; i++)
            list.Add(items[i].value);
    }

    public void GetItemsInShape(Func<Bounds, bool> ShapeFunction, List<T> list)
    {
        if (children.Length > 0)
        {
            foreach (var child in children)
                if (ShapeFunction(new Bounds(child.center, child.sideLength * Vector3.one)))
                    child.GetItemsInShape(ShapeFunction, list);
            return;
        }
        foreach (var item in items)
            if (ShapeFunction(new Bounds(item.key, Vector3.zero)))
                list.Add(item.value);
    }
    public void GetItemsInThickRay(Ray ray, float distance, float radius, List<T> list) => GetItemsInShape(bounds =>
    {
        bounds.Expand(radius);
        var intersects = bounds.IntersectRay(ray, out var d);
        return intersects && d <= distance;
    }, list);
    public void GetItemsInBounds(Bounds bounds, List<T> list) => GetItemsInShape(bounds.Intersects, list);
    public void GetItemsInRadius(Vector3 point, float radius, List<T> list) => GetItemsInShape(bounds => bounds.SqrDistance(point) <= radius * radius, list);
}

public interface IReadOnlySpatialOcttree<T>
{
    public void GetAll(List<T> list);
    public void GetItemsInShape(Func<Bounds, bool> ShapeFunction, List<T> list);
    public void GetItemsInBounds(Bounds bounds, List<T> list);
    public void GetItemsInRadius(Vector3 point, float radius, List<T> list);
    public void GetItemsInThickRay(Ray ray, float distance, float radius, List<T> list);
    public T this[Vector3 key] { get; }
}

