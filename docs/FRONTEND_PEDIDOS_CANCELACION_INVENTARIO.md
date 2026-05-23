# Guía frontend: pedidos unificados, `esPreparado` e inventario, cancelación con PIN

Documentación orientada al equipo de frontend para consumir los cambios del backend (API v1). Base URL típica: `/api/v1` (más el host del servidor).

---

## 1. Pedidos: listado unificado (mesa, delivery, llevar)

### Qué cambió

- **`GET /api/v1/pedidos`** ya **no excluye** delivery: devuelve **todos** los orígenes.
- Cada ítem incluye:
  - **`tipo`**: `"mesa"` | `"delivery"` | `"llevar"` (valor pensado para UI/filtros).
  - **`origenPedido`**: valor en BD (`Salon`, `Delivery`, `Llevar`).

### Filtro por tipo (query)

| Parámetro | Descripción |
|-----------|-------------|
| `tipo` | Opcional. `mesa`, `delivery` o `llevar`. Si se omite, se listan **todos**. |
| `estado`, `mesaId`, `meseroId`, `desde`, `hasta`, `page`, `pageSize` | Igual que antes. |

**Ejemplo:** solo delivery activos en la vista de reparto:

```http
GET /api/v1/pedidos?tipo=delivery&estado=Pendiente&page=1&pageSize=50
Authorization: Bearer <token>
```

**Ejemplo de ítem en la respuesta (dentro de `data.items`):**

```json
{
  "id": 42,
  "numero": "ORD-00001",
  "tipo": "mesa",
  "origenPedido": "Salon",
  "mesaId": 3,
  "mesa": 5,
  "estado": "Pendiente",
  "monto": 150.00,
  "fechaCreacion": "2026-04-08T10:00:00",
  "productosCount": 2
}
```

### Resumen

**`GET /api/v1/pedidos/resumen`** admite el mismo `?tipo=...`.

- Incluye **`pedidosPorTipo`**: `{ "mesa": n, "delivery": n, "llevar": n }` (conteos según los mismos filtros `estado`, fechas, etc.).

Útil para chips/KPIs en la pantalla de pedidos.

### Detalle por ID

**`GET /api/v1/pedidos/{id}`** ahora sirve también para pedidos **delivery** (antes solo salón).

- Incluye **`tipo`**, **`origenPedido`**.
- Si es delivery: pueden venir `clienteNombreDelivery`, `clienteTelefonoDelivery`, `clienteDireccionDelivery` (nombres exactos según contrato actual del API).

### Excel

**`GET /api/v1/pedidos/exportar-excel`** incluye columna **Tipo** y respeta `?tipo=...`.

### Pedidos “para llevar” (opcional)

- Origen en BD: **`Llevar`**.
- **POS:** al crear orden, **`POST /api/v1/pos/ordenes`** puede enviar **`"tipo": "llevar"`** (también acepta `"para llevar"`, `"takeout"`). El backend guarda `origenPedido = Llevar`.

---

## 2. Productos: `esPreparado` e inventario al cancelar

### Concepto (solo backend ejecuta la lógica; el frontend debe **mostrar/editar** el flag)

| `esPreparado` | Significado | Al **cancelar** un pedido con ese producto |
|---------------|-------------|---------------------------------------------|
| `true` | Comida preparada | **No** se devuelve stock (aunque el producto tenga `controlarStock`). |
| `false` | Bebida embotellada, etc. | Si `controlarStock` es `true`, **sí** se devuelve la cantidad al inventario. |

### API de productos

- En **listado**, **detalle**, **crear** y **actualizar** producto aparece **`esPreparado`** (camelCase en JSON típico del API).

**Crear / actualizar (admin):**

```json
{
  "nombre": "Coca Cola 600ml",
  "precio": 25,
  "controlarStock": true,
  "esPreparado": false
}
```

- Si en **crear** no envían `esPreparado`, el backend asume **`true`** (comportamiento seguro para comida).
- En **actualizar**, si no envían el campo, se puede **mantener** el valor anterior (según implementación: campo opcional `esPreparado`).

**Recomendación UI:** en catálogo de productos, checkbox o switch “Es preparado (cocina)” / “No devuelve stock al cancelar pedido”, con texto de ayuda para bebidas embotelladas.

---

## 3. Cancelación de pedidos con código (PIN)

### Configuración

- El código **no** se genera por pedido: es un **PIN global** guardado en **Configuraciones**.
- **Clave:** `PinCancelacionPedidos`.
- Si no existía, el servidor puede crear una entrada por defecto (p. ej. `0000`); en producción debe **cambiarlo** el administrador desde la pantalla de configuración (misma API/tab `Configuraciones` que el resto del sistema).

### Endpoints que deben enviar el cuerpo con el código

Todos usan **`POST`** y body JSON:

```json
{
  "codigo": "1234"
}
```

| Acción | Método y ruta |
|--------|----------------|
| Cancelar (unificado: mesa, delivery, llevar) | `POST /api/v1/pedidos/{id}/cancelar` |
| Cancelar solo delivery (misma lógica; compatibilidad) | `POST /api/v1/delivery/pedidos/{id}/cancelar` |
| Cancelar desde flujo POS | `POST /api/v1/pos/ordenes/{id}/cancelar` |

**Antes** (si el frontend usaba **PATCH** sin body en delivery): **ya no aplica**; debe ser **POST** con `codigo`.

### Respuestas

| HTTP | Significado |
|------|-------------|
| **200** | `success: true`, pedido cancelado. |
| **400** | Falta `codigo` o body inválido. |
| **403** | PIN incorrecto (`success: false`, mensaje tipo “Código de verificación inválido”). **No** se cancela el pedido. |
| **404** | Pedido no encontrado (según ruta). |
| **409** | No se puede cancelar (ej. ya pagado, ya cancelado). |
| **503** | PIN no configurado en sistema (mensaje de configuración pendiente). |

### Flujo recomendado en UI

1. Usuario elige “Cancelar pedido”.
2. Modal pide **código de autorización** (PIN).
3. Frontend envía `POST .../cancelar` con `{ "codigo": "<lo que escribió>" }`.
4. Si **403**: mostrar error claro (“Código incorrecto”) sin cerrar el pedido.
5. Si **200**: actualizar estado local y refrescar listas.

---

## Referencia rápida de headers

| Cabecera | Valor |
|----------|--------|
| `Authorization` | `Bearer <access_token>` |
| `Content-Type` | `application/json` en body de cancelación |

---

## Resumen para el equipo frontend

1. **Pedidos:** usar **`tipo`** y **`origenPedido`**; filtrar con **`?tipo=`**; usar **`pedidosPorTipo`** en resumen.
2. **Productos:** exponer **`esPreparado`** en ABM; marcar bebidas/reventa como `false` si deben devolver stock al cancelar.
3. **Cancelar:** siempre **`POST`** + **`{ "codigo": "..." }`**; tratar **403** como PIN inválido; PIN configurado en **Configuraciones** (`PinCancelacionPedidos`).
