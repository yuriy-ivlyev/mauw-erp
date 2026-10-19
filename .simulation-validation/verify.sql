\set ON_ERROR_STOP on
DO $$
DECLARE failures bigint;
BEGIN
    SELECT count(*) INTO failures FROM (
        SELECT m.warehouse_id, m.variant_id, sum(m.quantity) AS movement_quantity
        FROM stock_movements m GROUP BY m.warehouse_id, m.variant_id
    ) movements FULL JOIN stock_items stock
        ON stock.warehouse_id = movements.warehouse_id AND stock.product_variant_id = movements.variant_id
    WHERE coalesce(movements.movement_quantity, 0) <> coalesce(stock.quantity_on_hand, 0);
    IF failures <> 0 THEN RAISE EXCEPTION 'Stock/movement reconciliation failed: %', failures; END IF;

    SELECT count(*) INTO failures FROM (
        SELECT sum(quantity) OVER (PARTITION BY warehouse_id, variant_id ORDER BY movement_date_utc) quantity
        FROM stock_movements
    ) chronological WHERE quantity < 0;
    IF failures <> 0 THEN RAISE EXCEPTION 'Negative chronological stocks: %', failures; END IF;

    SELECT count(*) INTO failures FROM stock_movements m
    JOIN product_variants v ON v.id = m.variant_id JOIN products p ON p.id = v.product_id
    WHERE m.movement_date_utc < p.created_at;
    IF failures <> 0 THEN RAISE EXCEPTION 'Movements before introduction: %', failures; END IF;

    SELECT count(*) INTO failures FROM (
        SELECT m.warehouse_id, extract(year FROM m.movement_date_utc), sum(m.quantity) units
        FROM stock_movements m JOIN warehouses w ON w.id = m.warehouse_id
        WHERE w.is_sellable AND m.movement_date_utc < '2026-01-01'::timestamptz
        GROUP BY m.warehouse_id, extract(year FROM m.movement_date_utc)
        HAVING sum(m.quantity) <> 0
    ) previous_years;
    IF failures <> 0 THEN RAISE EXCEPTION 'Completed-year commercial stock: %', failures; END IF;

    SELECT count(*) INTO failures FROM invoices i
    FULL JOIN orders o ON o.id = i.order_id
    FULL JOIN ddt_documents d ON d.order_id = o.id
    WHERE i.id IS NULL OR o.id IS NULL OR d.id IS NULL
       OR i.contact_id <> o.contact_id OR d.contact_id <> o.contact_id
       OR o.order_date > d.ddt_date OR d.ddt_date > i.invoice_date
       OR i.taxable_total <> o.taxable_total OR i.vat_total <> o.vat_total OR i.gross_total <> o.gross_total
       OR i.taxable_total <> d.taxable_total OR i.vat_total <> d.vat_total OR i.gross_total <> d.gross_total;
    IF failures <> 0 THEN RAISE EXCEPTION 'Document linkage/totals mismatch: %', failures; END IF;

    SELECT count(*) INTO failures FROM invoices i
    JOIN (SELECT invoice_id, sum(line_net_total) net FROM invoice_items GROUP BY invoice_id) lines
        ON lines.invoice_id = i.id
    WHERE i.taxable_total <> lines.net OR i.vat_total <> round(lines.net * 22 / 100, 2)
        OR i.gross_total <> i.taxable_total + i.vat_total;
    IF failures <> 0 THEN RAISE EXCEPTION 'Fiscal reconciliation mismatch: %', failures; END IF;

    SELECT count(*) INTO failures FROM invoice_items i
    JOIN invoices doc ON doc.id = i.invoice_id
    FULL JOIN order_items o ON o.order_id = doc.order_id AND o.variant_id = i.variant_id
    LEFT JOIN ddt_documents d ON d.order_id = doc.order_id
    LEFT JOIN ddt_items di ON di.ddt_document_id = d.id AND di.variant_id = i.variant_id
    WHERE i.id IS NULL OR o.id IS NULL OR di.id IS NULL OR i.quantity <> o.quantity OR i.quantity <> di.quantity
       OR i.unit_net_price <> o.unit_net_price OR i.unit_net_price <> di.unit_net_price
       OR i.discount_percent <> o.discount_percent OR i.discount_percent <> di.discount_percent
       OR i.line_net_total <> o.line_net_total OR i.line_net_total <> di.line_net_total
       OR i.line_vat_total IS NOT NULL OR i.line_gross_total IS NOT NULL;
    IF failures <> 0 THEN RAISE EXCEPTION 'Document line mismatch: %', failures; END IF;

    IF (SELECT count(DISTINCT contact_id) FROM invoices) <>
       (SELECT count(*) FROM customer_profiles cp JOIN contacts c ON c.id = cp.contact_id WHERE c.status = 1)
    THEN RAISE EXCEPTION 'Active customer coverage mismatch'; END IF;

    IF (SELECT count(*) * 10 FROM customer_profiles WHERE default_agent_id IS NOT NULL) <>
       (SELECT count(*) * 3 FROM customer_profiles)
    THEN RAISE EXCEPTION 'Customer agent coverage is not 30 percent'; END IF;

    IF (SELECT count(*) * 10 FROM customer_profiles cp JOIN contacts c ON c.id = cp.contact_id WHERE c.status = 1 AND default_agent_id IS NOT NULL) <>
       (SELECT count(*) * 3 FROM customer_profiles cp JOIN contacts c ON c.id = cp.contact_id WHERE c.status = 1)
    THEN RAISE EXCEPTION 'Active customer agent coverage is not 30 percent'; END IF;

    SELECT count(*) INTO failures FROM products p
    JOIN (SELECT product_id, count(*) n, count(DISTINCT size_id) sizes, count(DISTINCT color_id) colors FROM product_variants GROUP BY product_id) v
        ON v.product_id = p.id
    WHERE v.n NOT IN (35, 42) OR v.sizes <> 7 OR v.colors NOT IN (5, 6)
       OR p.base_purchase_cost < 5 OR p.base_purchase_cost > 40
       OR p.selling_price <> ceil(p.base_purchase_cost * 1.4 * 10) / 10;
    IF failures <> 0 THEN RAISE EXCEPTION 'Catalog cardinality/pricing mismatch: %', failures; END IF;
END $$;

SELECT 'integration_verified' AS result,
    (SELECT count(*) FROM products) AS models,
    (SELECT count(*) FROM product_variants) AS variants,
    (SELECT count(*) FROM invoices) AS invoices,
    (SELECT count(*) FROM stock_movements) AS movements,
    pg_size_pretty(pg_database_size(current_database())) AS database_size;
